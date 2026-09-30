using Almacen.DTOs;
using Almacen.Models;
using ClosedXML.Excel;

namespace Almacen.Services;

/// <summary>
/// Procesa plantillas oficiales y genera reportes normativos con datos reales.
/// Los archivos se guardan como BYTEA en BD (persisten en Railway).
/// El motor mapea por NOMBRE DE COLUMNA.
/// </summary>
public class PlantillaNormativaService
{
    public PlantillaNormativaService()
    {
    }

    // ═══════════════════════════════════════════════════════════
    // PROCESAR PLANTILLA (valida y convierte CSV → XLSX si aplica)
    // ═══════════════════════════════════════════════════════════
    public async Task<(bool Ok, byte[] Contenido, string NombreArchivo, string Mensaje)>
        ProcesarPlantillaAsync(Stream contenido, string nombreOriginal)
    {
        try
        {
            var ext = Path.GetExtension(nombreOriginal).ToLowerInvariant();
            if (ext != ".xlsx" && ext != ".xls" && ext != ".csv")
                return (false, Array.Empty<byte>(), "", "Solo se permiten archivos .xlsx, .xls o .csv.");

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
                try
                {
                    bytesFinales = ConvertirCsvAXlsx(bytes);
                }
                catch (Exception ex)
                {
                    return (false, Array.Empty<byte>(), "", $"Error al procesar CSV: {ex.Message}");
                }
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

    // ═══════════════════════════════════════════════════════════
    // CONVERTIR CSV → XLSX
    // ═══════════════════════════════════════════════════════════
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
            {
                ws.Cell(fila, c + 1).Value = celdas[c];
            }
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
            if (ch == '"')
            {
                entreComillas = !entreComillas;
            }
            else if (ch == separador && !entreComillas)
            {
                celdas.Add(actual.ToString().Trim());
                actual.Clear();
            }
            else
            {
                actual.Append(ch);
            }
        }

        celdas.Add(actual.ToString().Trim());
        return celdas;
    }

    // ═══════════════════════════════════════════════════════════
    // GENERAR REPORTE (desde bytes de la plantilla)
    // ═══════════════════════════════════════════════════════════
    public byte[] GenerarReporte(
        byte[] plantillaBytes,
        List<Bien> bienes,
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

        int fila = filaHeader + 1;
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
    private static int DetectarFilaHeader(IXLWorksheet ws)
    {
        var ultimaFila = ws.LastRowUsed()?.RowNumber() ?? 0;
        var limite = Math.Min(15, ultimaFila);

        for (int f = 1; f <= limite; f++)
        {
            var celdasConTexto = 0;
            var ultimaCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;

            for (int c = 1; c <= ultimaCol; c++)
            {
                if (!string.IsNullOrWhiteSpace(ws.Cell(f, c).GetString()))
                    celdasConTexto++;
            }

            if (celdasConTexto >= 3)
                return f;
        }

        return -1;
    }

    private static XLCellValue? ObtenerValorPorColumna(string nombreColumna, Bien b)
    {
        var n = nombreColumna.ToUpperInvariant().Trim();

        n = n.Replace("Á", "A").Replace("É", "E").Replace("Í", "I")
             .Replace("Ó", "O").Replace("Ú", "U").Replace("Ñ", "N");

        return n switch
        {
            var x when x.Contains("CODIGO CGN") => b.CodigoCgn ?? "-",
            var x when x.Contains("CODIGO BIEN") => b.Codigo ?? "",
            var x when x == "CODIGO" => b.Codigo ?? "",
            var x when x.Contains("NOMBRE DEL BIEN") => b.Nombre ?? "",
            var x when x == "NOMBRE" => b.Nombre ?? "",
            var x when x == "CONCEPTO" => b.Nombre ?? "",
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

            var x when x.Contains("SALDO INICIAL") => 0,
            var x when x.Contains("MOVIMIENTO DEBITO") => b.ValorAdquisicion,
            var x when x.Contains("MOVIMIENTO CREDITO") => b.DepreciacionAcumulada,
            var x when x.Contains("SALDO FINAL") => b.ValorNeto,

            _ => null
        };
    }
}