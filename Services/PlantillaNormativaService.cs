using Almacen.DTOs;
using Almacen.Models;
using Almacen.Interfaces;
using ClosedXML.Excel;

namespace Almacen.Services;

public class PlantillaNormativaService
{
    public PlantillaNormativaService() { }

    // ═══════════════════════════════════════════════════════════
    // PROCESAR PLANTILLA
    // ═══════════════════════════════════════════════════════════
    public async Task<(bool Ok, byte[] Contenido, string NombreArchivo, string Mensaje)>
        ProcesarPlantillaAsync(Stream contenido, string nombreOriginal)
    {
        try
        {
            var ext = Path.GetExtension(nombreOriginal).ToLowerInvariant();
            if (ext != ".xlsx" && ext != ".xls" && ext != ".csv")
                return (false, Array.Empty<byte>(), "", "Solo se permiten .xlsx, .xls o .csv.");

            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                await contenido.CopyToAsync(ms);
                bytes = ms.ToArray();
            }

            if (bytes.Length == 0)
                return (false, Array.Empty<byte>(), "", "El archivo está vacío.");

            byte[] bytesFinales;

            if (ext == ".csv")
            {
                try { bytesFinales = ConvertirCsvAXlsx(bytes); }
                catch (Exception ex) { return (false, Array.Empty<byte>(), "", $"Error CSV: {ex.Message}"); }
            }
            else
            {
                try
                {
                    using var msValidar = new MemoryStream(bytes);
                    using var wb = new XLWorkbook(msValidar);
                    if (!wb.Worksheets.Any())
                        return (false, Array.Empty<byte>(), "", "El archivo no tiene hojas.");
                }
                catch
                {
                    return (false, Array.Empty<byte>(), "", "El archivo no es un Excel válido.");
                }
                bytesFinales = bytes;
            }

            return (true, bytesFinales, nombreOriginal, "");
        }
        catch (Exception ex)
        {
            return (false, Array.Empty<byte>(), "", $"Error: {ex.Message}");
        }
    }

    private static byte[] ConvertirCsvAXlsx(byte[] csvBytes)
    {
        var texto = System.Text.Encoding.UTF8.GetString(csvBytes);
        var lineas = texto.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Hoja1");

        int fila = 1;
        foreach (var linea in lineas)
        {
            if (string.IsNullOrWhiteSpace(linea)) { fila++; continue; }
            var separador = linea.Contains(';') ? ';' : ',';
            var celdas = SepararCsv(linea, separador);
            for (int c = 0; c < celdas.Count; c++)
                ws.Cell(fila, c + 1).Value = celdas[c];
            fila++;
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Flush();
        ms.Position = 0;
        var bytes = ms.ToArray();
        ms.Close();
        return bytes;
    }

    private static List<string> SepararCsv(string linea, char separador)
    {
        var celdas = new List<string>();
        var actual = new System.Text.StringBuilder();
        bool entreComillas = false;

        foreach (var ch in linea)
        {
            if (ch == '"') entreComillas = !entreComillas;
            else if (ch == separador && !entreComillas)
            {
                celdas.Add(actual.ToString().Trim());
                actual.Clear();
            }
            else actual.Append(ch);
        }

        celdas.Add(actual.ToString().Trim());
        return celdas;
    }

    // ═══════════════════════════════════════════════════════════
    // GENERAR REPORTE
    // ═══════════════════════════════════════════════════════════
    public byte[] GenerarReporte(
        byte[] plantillaBytes,
        List<Bien> bienes,
        List<EntradaConBienDTO> entradas,
        List<MovimientoKardexReporteDTO> kardex,
        string nombreInstitucion,
        string nombreUsuario)
    {
        using var wb = new XLWorkbook(new MemoryStream(plantillaBytes));
        var ws = wb.Worksheets.First();

        int filaHeader = DetectarFilaHeader(ws);
        if (filaHeader <= 0)
            throw new InvalidOperationException("No se detectaron encabezados en la plantilla.");

        var columnas = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var ultimaCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;

        for (int c = 1; c <= ultimaCol; c++)
        {
            var texto = ws.Cell(filaHeader, c).GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(texto))
                columnas[texto] = c;
        }

        bool esAgrupadoPorCgn = columnas.Keys.Any(k =>
            Normalizar(k).Contains("SALDO INICIAL") ||
            Normalizar(k).Contains("MOVIMIENTO DEBITO"));

        bool esFubConEntradas = columnas.Keys.Any(k =>
            Normalizar(k).Contains("NUMERO FACTURA") ||
            Normalizar(k).Contains("NIT PROVEEDOR"));

        int fila = filaHeader + 1;

        if (esAgrupadoPorCgn)
        {
            var nombresOficialesCgn = new Dictionary<string, string>
            {
                { "163501", "Materiales y suministros" },
                { "163502", "Papelería" },
                { "163503", "Aseo y cafetería" },
                { "163504", "Equipos de comunicación" },
                { "163505", "Equipos de laboratorio" },
                { "163506", "Mobiliario" },
                { "163507", "Equipos de cómputo" },
                { "163508", "Equipos deportivos" },
                { "163509", "Herramientas" },
                { "163510", "Otros" },
                { "1905",   "Categoría especial 1905" }
            };

            var grupoDevolutivos = bienes
                .GroupBy(b => b.CodigoCgn ?? "SIN-CGN")
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var activos = g.Where(x => x.Activo).ToList();
                        var dadosDeBaja = g.Where(x => !x.Activo).ToList();
                        return (
                            Debito: g.Sum(x => x.ValorAdquisicion),
                            Credito: dadosDeBaja.Sum(x => x.ValorAdquisicion),
                            SaldoFinal: activos.Sum(x => x.ValorAdquisicion)
                        );
                    });

            var grupoConsumibles = kardex
                .Where(k => !string.IsNullOrWhiteSpace(k.CodigoCgn))
                .GroupBy(k => k.CodigoCgn!)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var debito = g.Where(m =>
                            m.TipoMovimiento == "ENTRADA" ||
                            m.TipoMovimiento == "AJUSTE_POS")
                            .Sum(m => m.ValorMovimiento);

                        var credito = g.Where(m =>
                            m.TipoMovimiento == "SALIDA" ||
                            m.TipoMovimiento == "AJUSTE_NEG")
                            .Sum(m => m.ValorMovimiento);

                        return (
                            Debito: debito,
                            Credito: credito,
                            SaldoFinal: debito - credito
                        );
                    });

            var codigosUnicos = grupoDevolutivos.Keys
                .Union(grupoConsumibles.Keys)
                .OrderBy(c => c)
                .ToList();

            var gruposFinales = codigosUnicos.Select(cgn =>
            {
                grupoDevolutivos.TryGetValue(cgn, out var dev);
                grupoConsumibles.TryGetValue(cgn, out var cons);

                return new
                {
                    Cgn = cgn,
                    Concepto = nombresOficialesCgn.TryGetValue(cgn, out var n)
                        ? n
                        : (bienes.FirstOrDefault(b => b.CodigoCgn == cgn)?.CategoriaNombre
                            ?? "Sin categoría"),

                    MovDebito = dev.Debito + cons.Debito,
                    MovCredito = dev.Credito + cons.Credito,
                    SaldoFinal = dev.SaldoFinal + cons.SaldoFinal
                };
            }).ToList();

            foreach (var g in gruposFinales)
            {
                foreach (var (nombreCol, colIdx) in columnas)
                {
                    var n = Normalizar(nombreCol);
                    object? valor = null;

                    if (n == "CODIGO" || n.Contains("CODIGO CGN"))
                        valor = g.Cgn;
                    else if (n == "CONCEPTO" || n.Contains("NOMBRE"))
                        valor = g.Concepto;
                    else if (n.Contains("SALDO INICIAL"))
                        valor = 0m;
                    else if (n.Contains("MOVIMIENTO DEBITO"))
                        valor = g.MovDebito;
                    else if (n.Contains("MOVIMIENTO CREDITO"))
                        valor = g.MovCredito;
                    else if (n.Contains("SALDO FINAL"))
                        valor = g.SaldoFinal;

                    if (valor is not null)
                        ws.Cell(fila, colIdx).Value = XLCellValue.FromObject(valor);
                }
                fila++;
            }
        }
        else if (esFubConEntradas)
        {
            var entradasPorBien = entradas
                .GroupBy(e => e.BienId)
                .ToDictionary(g => g.Key, g => g.First());

            var bienesConSoporte = bienes
                .Where(b => entradasPorBien.ContainsKey(b.Id))
                .ToList();

            foreach (var b in bienesConSoporte)
            {
                entradasPorBien.TryGetValue(b.Id, out var entrada);

                foreach (var (nombreCol, colIdx) in columnas)
                {
                    var valor = ObtenerValorFubDevolutivo(nombreCol, b, entrada);
                    if (valor is not null)
                        ws.Cell(fila, colIdx).Value = valor.Value;
                }
                fila++;
            }

            foreach (var k in kardex)
            {
                foreach (var (nombreCol, colIdx) in columnas)
                {
                    var valor = ObtenerValorFubConsumo(nombreCol, k);
                    if (valor is not null)
                        ws.Cell(fila, colIdx).Value = valor.Value;
                }
                fila++;
            }
        }
        else
        {
            foreach (var b in bienes)
            {
                foreach (var (nombreCol, colIdx) in columnas)
                {
                    var valor = ObtenerValorPorColumna(nombreCol, b);
                    if (valor is not null)
                        ws.Cell(fila, colIdx).Value = valor.Value;
                }
                fila++;
            }
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Flush();
        ms.Position = 0;
        var outBytes = ms.ToArray();
        ms.Close();
        return outBytes;
    }

    // ═══════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════
    private static string Normalizar(string s) =>
        s.ToUpperInvariant().Trim()
         .Replace("Á", "A").Replace("É", "E").Replace("Í", "I")
         .Replace("Ó", "O").Replace("Ú", "U").Replace("Ñ", "N");

    private static int DetectarFilaHeader(IXLWorksheet ws)
    {
        var ultimaFila = ws.LastRowUsed()?.RowNumber() ?? 0;
        var limite = Math.Min(15, ultimaFila);

        for (int f = 1; f <= limite; f++)
        {
            var celdasConTexto = 0;
            var ultimaCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
            for (int c = 1; c <= ultimaCol; c++)
                if (!string.IsNullOrWhiteSpace(ws.Cell(f, c).GetString())) celdasConTexto++;
            if (celdasConTexto >= 3) return f;
        }
        return -1;
    }

    private static XLCellValue? ObtenerValorPorColumna(string nombreColumna, Bien b)
    {
        var n = Normalizar(nombreColumna);

        return n switch
        {
            var x when x.Contains("CODIGO CGN") => b.CodigoCgn ?? "-",
            var x when x.Contains("CODIGO BIEN") => b.Codigo ?? "",
            var x when x == "CODIGO" => b.Codigo ?? "",
            var x when x.Contains("NOMBRE DEL BIEN") => b.Nombre ?? "",
            var x when x == "NOMBRE" => b.Nombre ?? "",
            var x when x == "CATEGORIA" => b.CategoriaNombre ?? "",

            var x when x == "MARCA" => b.Marca ?? "",
            var x when x == "MODELO" => b.Modelo ?? "",
            var x when x == "SERIE" => b.Serie ?? "",

            var x when x == "CANTIDAD" => b.Cantidad,
            var x when x.Contains("VALOR ADQUISICION") => b.ValorAdquisicion,
            var x when x.Contains("VALOR ADQ") => b.ValorAdquisicion,
            var x when x.Contains("DEPRECIACION ACUM") => b.DepreciacionAcumulada,
            var x when x.Contains("DEPREC ACUM") => b.DepreciacionAcumulada,
            var x when x.Contains("VALOR NETO") => b.ValorNeto,

            var x when x.Contains("FECHA ADQUISICION") => b.FechaAdquisicion?.ToString("dd/MM/yyyy") ?? "",
            var x when x.Contains("FECHA ADQ") => b.FechaAdquisicion?.ToString("dd/MM/yyyy") ?? "",

            var x when x.Contains("UBICACION") => b.AulaNombre ?? b.Ubicacion ?? "",
            var x when x.Contains("RESPONSABLE") => b.FuncionarioNombre ?? b.Responsable ?? "",
            var x when x.Contains("FUNCIONARIO") => b.FuncionarioNombre ?? "",

            var x when x == "ESTADO" => b.EstadoFisico ?? "",
            var x when x.Contains("ESTADO FISICO") => b.EstadoFisico ?? "",
            var x when x == "ACTIVO" => b.Activo ? "Sí" : "No",

            _ => null
        };
    }

    private static XLCellValue? ObtenerValorFubDevolutivo(string nombreColumna, Bien b, EntradaConBienDTO? entrada)
    {
        var n = Normalizar(nombreColumna);

        return n switch
        {
            var x when x == "CODIGO" => b.Codigo ?? "",
            var x when x.Contains("CODIGO BIEN") => b.Codigo ?? "",
            var x when x.Contains("CODIGO CGN") => b.CodigoCgn ?? "-",
            var x when x.Contains("NOMBRE") => b.Nombre ?? "",
            var x when x == "MARCA" => b.Marca ?? "",
            var x when x == "MODELO" => b.Modelo ?? "",
            var x when x == "SERIE" => b.Serie ?? "",

            var x when x.Contains("TIPO MOVIMIENTO") => "ADQUISICION",
            var x when x == "FECHA" => entrada?.FechaEntrada?.ToString("dd/MM/yyyy")
                ?? b.FechaAdquisicion?.ToString("dd/MM/yyyy") ?? "",
            var x when x == "VALOR" => entrada?.Valor ?? b.ValorAdquisicion,

            var x when x.Contains("NIT PROVEEDOR") => entrada?.ProveedorNit ?? "",
            var x when x.Contains("NUMERO FACTURA") => entrada?.NumeroFactura ?? "",
            var x when x.Contains("FACTURA") => entrada?.NumeroFactura ?? "",
            var x when x.Contains("PROVEEDOR") => entrada?.ProveedorNombre ?? "",

            var x when x.Contains("UBICACION") => b.AulaNombre ?? b.Ubicacion ?? "",

            var x when x.Contains("RESPONSABLE") =>
                b.FuncionarioNombre
                ?? entrada?.FuncionarioRecibeNombre
                ?? b.Responsable
                ?? "",

            var x when x.Contains("FUNCIONARIO") =>
                b.FuncionarioNombre
                ?? entrada?.FuncionarioRecibeNombre
                ?? "",

            var x when x.Contains("OBSERVACION") => entrada?.Observaciones ?? "",

            var x when x.Contains("VALOR ADQUISICION") => b.ValorAdquisicion,
            var x when x.Contains("DEPRECIACION ACUM") => b.DepreciacionAcumulada,
            var x when x.Contains("VALOR NETO") => b.ValorNeto,

            _ => null
        };
    }

    private static XLCellValue? ObtenerValorFubConsumo(string nombreColumna, MovimientoKardexReporteDTO k)
    {
        var n = Normalizar(nombreColumna);

        return n switch
        {
            var x when x == "CODIGO" => k.BienCodigo ?? "",
            var x when x.Contains("CODIGO BIEN") => k.BienCodigo ?? "",
            var x when x.Contains("CODIGO CGN") => k.CodigoCgn ?? "-",
            var x when x.Contains("NOMBRE") => k.BienNombre ?? "",

            var x when x.Contains("TIPO MOVIMIENTO") => k.TipoMovimiento,

            var x when x == "FECHA" => k.FechaMovimiento.ToString("dd/MM/yyyy"),
            var x when x == "VALOR" => k.ValorMovimiento,

            var x when x.Contains("NIT PROVEEDOR") => k.ProveedorNit ?? "",
            var x when x.Contains("NUMERO FACTURA") => k.DocumentoReferencia ?? "",
            var x when x.Contains("FACTURA") => k.DocumentoReferencia ?? "",
            var x when x.Contains("PROVEEDOR") => k.ProveedorNombre ?? "",

            var x when x.Contains("UBICACION") => "",
            var x when x.Contains("RESPONSABLE") => k.FuncionarioNombre ?? "",
            var x when x.Contains("FUNCIONARIO") => k.FuncionarioNombre ?? "",
            var x when x.Contains("OBSERVACION") => k.Observaciones ?? "",

            var x when x == "MARCA" => "",
            var x when x == "MODELO" => "",
            var x when x == "SERIE" => "",
            var x when x.Contains("CANTIDAD") => k.Cantidad,
            var x when x.Contains("VALOR ADQUISICION") => k.ValorMovimiento,
            var x when x.Contains("DEPRECIACION") => 0m,
            var x when x.Contains("VALOR NETO") => k.ValorMovimiento,

            _ => null
        };
    }
}

// ═══════════════════════════════════════════════════════════════
// DTOs DE APOYO
// ═══════════════════════════════════════════════════════════════

public class EntradaConBienDTO
{
    public int BienId { get; set; }
    public string TipoFuente { get; set; } = "";
    public string? NumeroFactura { get; set; }
    public DateTime? FechaEntrada { get; set; }
    public decimal Valor { get; set; }
    public string? ProveedorNombre { get; set; }
    public string? ProveedorNit { get; set; }
    public string? Observaciones { get; set; }
    public string? FuncionarioRecibeNombre { get; set; }
}

public class MovimientoKardexReporteDTO
{
    public int Id { get; set; }
    public int BienId { get; set; }
    public string BienCodigo { get; set; } = "";
    public string BienNombre { get; set; } = "";
    public string? CodigoCgn { get; set; }
    public string? CategoriaNombre { get; set; }
    public string TipoMovimiento { get; set; } = "";
    public DateTime FechaMovimiento { get; set; }
    public decimal Cantidad { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal ValorMovimiento { get; set; }
    public string? DocumentoReferencia { get; set; }
    public string? ProveedorNombre { get; set; }
    public string? ProveedorNit { get; set; }
    public string? FuncionarioNombre { get; set; }
    public string? Observaciones { get; set; }
}