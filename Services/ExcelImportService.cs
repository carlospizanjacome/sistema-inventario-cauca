using Almacen.DTOs;
using Almacen.Interfaces;
using Almacen.Models;
using ClosedXML.Excel;
using Dapper;
using Npgsql;
using System.Globalization;
using System.Text;

namespace Almacen.Services;

/// <summary>
/// Service para importación masiva de Excel/CSV (V2.0 §12).
/// Lee archivos .xlsx, .xls y .csv, valida filas y las persiste como bienes reales.
/// Aplica RECHAZO INTELIGENTE de duplicados por código contable.
/// Mensaje final honesto: distingue bienes nuevos vs ya existentes.
/// </summary>
public class ExcelImportService
{
    private readonly string _cs;
    private readonly IBienRepository _bienRepo;
    private readonly IImportacionRepository _impRepo;
    private readonly UsuarioSesionService _sesion;

    public ExcelImportService(
        IConfiguration cfg,
        IBienRepository bienRepo,
        IImportacionRepository impRepo,
        UsuarioSesionService sesion)
    {
        _cs = cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta DefaultConnection.");
        _bienRepo = bienRepo;
        _impRepo = impRepo;
        _sesion = sesion;
    }

    // ═══════════════════════════════════════════════════════
    // MAPEO DE CAMPOS
    // ═══════════════════════════════════════════════════════
    private static readonly Dictionary<string, string[]> MapeoColumnas = new()
    {
        ["nombre"] = new[] { "nombre", "bien", "descripcion", "articulo", "elemento", "detalle" },
        ["codigo"] = new[] { "codigo", "cod", "placa", "codigo_bien", "codigo_inventario", "codigo_contable" },
        ["categoria"] = new[] { "categoria", "categoría", "tipo", "clase", "grupo" },
        ["tipo_bien"] = new[] { "tipo_bien", "tipobien", "tipo de bien", "clase bien" },
        ["valor_adquisicion"] = new[] { "valor", "valor_adquisicion", "valor_adq", "precio", "costo", "valor compra" },
        ["fecha_adquisicion"] = new[] { "fecha", "fecha_adquisicion", "fecha_adq", "fecha_compra", "fecha ingreso" },
        ["marca"] = new[] { "marca", "fabricante" },
        ["modelo"] = new[] { "modelo", "referencia" },
        ["serie"] = new[] { "serie", "serial", "n_serie", "no_serie", "imei" },
        ["estado_fisico"] = new[] { "estado", "estado_fisico", "condicion" },
        ["aula"] = new[] { "aula", "ubicacion", "salon", "espacio", "dependencia" },
        ["funcionario"] = new[] { "funcionario", "responsable", "cuentadante", "encargado" },
        ["observaciones"] = new[] { "observaciones", "obs", "notas", "comentarios" },
        ["unidad_medida"] = new[] { "unidad", "unidad_medida", "um", "medida" },
        ["stock_inicial"] = new[] { "stock", "stock_inicial", "cantidad", "existencia", "existencia inicial" },
        ["stock_minimo"] = new[] { "stock_minimo", "minimo", "min" },
        ["fuente_financiacion"] = new[] { "fuente", "fuente_financiacion", "fse", "donacion", "origen" }
    };

    // ═══════════════════════════════════════════════════════
    // 1. LEER ARCHIVO — Preview
    // ═══════════════════════════════════════════════════════
    public async Task<PreviewExcelDTO> LeerExcelAsync(Stream stream, string nombreArchivo)
    {
        return await Task.Run(() =>
        {
            var resultado = new PreviewExcelDTO();
            var extension = Path.GetExtension(nombreArchivo).ToLowerInvariant();

            if (extension == ".csv")
                return LeerCsvPreview(stream, resultado);

            using var wb = new XLWorkbook(stream);

            foreach (var ws in wb.Worksheets)
                resultado.Hojas.Add(ws.Name);

            var primeraHoja = wb.Worksheets.First();
            resultado.HojaSeleccionada = primeraHoja.Name;

            int filaHeader = DetectarFilaHeader(primeraHoja);
            if (filaHeader <= 0)
                throw new InvalidOperationException("No se detectaron encabezados en el archivo.");

            var ultimaCol = primeraHoja.LastColumnUsed()?.ColumnNumber() ?? 0;
            for (int c = 1; c <= ultimaCol; c++)
            {
                var texto = primeraHoja.Cell(filaHeader, c).GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(texto))
                    resultado.Columnas.Add(texto);
            }

            var ultimaFila = primeraHoja.LastRowUsed()?.RowNumber() ?? 0;
            int filasLeidas = 0;

            for (int f = filaHeader + 1; f <= ultimaFila && filasLeidas < 10; f++)
            {
                var fila = new Dictionary<string, string>();

                for (int c = 1; c <= resultado.Columnas.Count; c++)
                {
                    var colNombre = resultado.Columnas[c - 1];
                    var valor = primeraHoja.Cell(f, c).GetString()?.Trim() ?? "";
                    fila[colNombre] = valor;
                }

                if (fila.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
                {
                    resultado.PrimerasFilas.Add(fila);
                    filasLeidas++;
                }
            }

            resultado.TotalFilasEstimadas = Math.Max(0, ultimaFila - filaHeader);
            return resultado;
        });
    }

    private PreviewExcelDTO LeerCsvPreview(Stream stream, PreviewExcelDTO resultado)
    {
        resultado.Hojas.Add("CSV");

        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        var lineas = new List<string>();
        string? linea;

        while ((linea = reader.ReadLine()) != null)
        {
            if (!string.IsNullOrWhiteSpace(linea))
                lineas.Add(linea);
        }

        if (lineas.Count == 0)
            throw new InvalidOperationException("El archivo CSV está vacío.");

        var primeraLinea = lineas[0];
        var sep = primeraLinea.Count(c => c == ';') > primeraLinea.Count(c => c == ',') ? ';' : ',';

        var headers = SepararCsv(lineas[0], sep);
        resultado.Columnas.AddRange(headers);
        resultado.HojaSeleccionada = "CSV";

        for (int i = 1; i < lineas.Count && i <= 10; i++)
        {
            var celdas = SepararCsv(lineas[i], sep);
            var fila = new Dictionary<string, string>();

            for (int c = 0; c < headers.Count; c++)
            {
                var valor = c < celdas.Count ? celdas[c] : "";
                fila[headers[c]] = valor;
            }

            resultado.PrimerasFilas.Add(fila);
        }

        resultado.TotalFilasEstimadas = lineas.Count - 1;
        return resultado;
    }

    // ═══════════════════════════════════════════════════════
    // 2. LEER TODAS LAS FILAS
    // ═══════════════════════════════════════════════════════
    public async Task<List<FilaImportacionDTO>> LeerTodasLasFilasAsync(
        Stream stream, string nombreHoja, MapeoColumnasDTO mapeo)
    {
        return await Task.Run(() =>
        {
            var filas = new List<FilaImportacionDTO>();

            if (nombreHoja == "CSV")
            {
                using var reader = new StreamReader(stream, Encoding.UTF8, true);
                var lineas = new List<string>();
                string? linea;
                while ((linea = reader.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(linea))
                        lineas.Add(linea);
                }

                if (lineas.Count < 2) return filas;

                var sep = lineas[0].Count(c => c == ';') > lineas[0].Count(c => c == ',') ? ';' : ',';
                var headers = SepararCsv(lineas[0], sep);

                for (int i = 1; i < lineas.Count; i++)
                {
                    var celdas = SepararCsv(lineas[i], sep);
                    var fila = new FilaImportacionDTO
                    {
                        NumeroFila = i + 1,
                        DatosCrudos = new Dictionary<string, string>()
                    };

                    for (int c = 0; c < headers.Count; c++)
                    {
                        fila.DatosCrudos[headers[c]] = c < celdas.Count ? celdas[c] : "";
                    }

                    AplicarMapeo(fila, mapeo);
                    filas.Add(fila);
                }

                return filas;
            }

            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheet(nombreHoja);

            int filaHeader = DetectarFilaHeader(ws);
            if (filaHeader <= 0)
                throw new InvalidOperationException("No se detectaron encabezados en la hoja.");

            var columnasIdx = new Dictionary<string, int>();
            var ultimaCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;

            for (int c = 1; c <= ultimaCol; c++)
            {
                var texto = ws.Cell(filaHeader, c).GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(texto))
                    columnasIdx[texto] = c;
            }

            var ultimaFila = ws.LastRowUsed()?.RowNumber() ?? 0;

            for (int f = filaHeader + 1; f <= ultimaFila; f++)
            {
                var fila = new FilaImportacionDTO
                {
                    NumeroFila = f,
                    DatosCrudos = new Dictionary<string, string>()
                };

                foreach (var (colNombre, colIdx) in columnasIdx)
                {
                    var valor = ws.Cell(f, colIdx).GetString()?.Trim() ?? "";
                    fila.DatosCrudos[colNombre] = valor;
                }

                if (fila.DatosCrudos.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
                {
                    AplicarMapeo(fila, mapeo);
                    filas.Add(fila);
                }
            }

            return filas;
        });
    }

    // ═══════════════════════════════════════════════════════
    // 3. APLICAR MAPEO
    // ═══════════════════════════════════════════════════════
    private static void AplicarMapeo(FilaImportacionDTO fila, MapeoColumnasDTO mapeo)
    {
        foreach (var (campoDestino, columnaExcel) in mapeo.Mapeo)
        {
            if (string.IsNullOrWhiteSpace(columnaExcel)) continue;
            if (!fila.DatosCrudos.TryGetValue(columnaExcel, out var valor)) continue;

            valor = valor?.Trim() ?? "";

            switch (campoDestino)
            {
                case "nombre": fila.Nombre = valor; break;
                case "codigo": fila.Codigo = valor; break;
                case "categoria": fila.CategoriaNombre = valor; break;
                case "tipo_bien": fila.TipoBien = valor; break;
                case "valor_adquisicion": fila.ValorAdquisicion = ParseDecimal(valor); break;
                case "fecha_adquisicion": fila.FechaAdquisicion = ParseFecha(valor); break;
                case "marca": fila.Marca = valor; break;
                case "modelo": fila.Modelo = valor; break;
                case "serie": fila.Serie = valor; break;
                case "estado_fisico": fila.EstadoFisico = valor; break;
                case "aula": fila.AulaNombre = valor; break;
                case "funcionario": fila.FuncionarioNombre = valor; break;
                case "observaciones": fila.Observaciones = valor; break;
                case "unidad_medida": fila.UnidadMedida = valor; break;
                case "stock_inicial": fila.StockInicial = ParseDecimal(valor); break;
                case "stock_minimo": fila.StockMinimo = ParseDecimal(valor); break;
                case "fuente_financiacion": fila.FuenteFinanciacion = valor; break;
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    // 4. AUTO-DETECTAR MAPEO
    // ═══════════════════════════════════════════════════════
    public MapeoColumnasDTO SugerirMapeo(IEnumerable<string> columnasExcel)
    {
        var mapeo = new MapeoColumnasDTO();

        foreach (var campoDestino in MapeoColumnas.Keys)
        {
            mapeo.Mapeo[campoDestino] = "";
        }

        var columnasNormalizadas = columnasExcel
            .Select(c => (Original: c, Normalizada: Normalizar(c)))
            .ToList();

        foreach (var (campoDestino, alias) in MapeoColumnas)
        {
            foreach (var aliasItem in alias)
            {
                var aliasNorm = Normalizar(aliasItem);
                var match = columnasNormalizadas.FirstOrDefault(c => c.Normalizada == aliasNorm);
                if (match.Original != null)
                {
                    mapeo.Mapeo[campoDestino] = match.Original;
                    break;
                }
            }
        }

        return mapeo;
    }

    // ═══════════════════════════════════════════════════════
    // 5. VALIDAR FILAS
    // ═══════════════════════════════════════════════════════
    public void ValidarFilas(List<FilaImportacionDTO> filas)
    {
        foreach (var fila in filas)
        {
            fila.Errores.Clear();
            fila.Advertencias.Clear();

            if (string.IsNullOrWhiteSpace(fila.Nombre))
                fila.Errores.Add("Nombre es obligatorio");

            if (string.IsNullOrWhiteSpace(fila.CategoriaNombre))
                fila.Errores.Add("Categoría es obligatoria");

            if (fila.ValorAdquisicion is null && !EsConsumo(fila))
                fila.Errores.Add("Valor de adquisición es obligatorio");

            if (fila.ValorAdquisicion.HasValue && fila.ValorAdquisicion < 0)
                fila.Errores.Add("Valor no puede ser negativo");

            if (string.IsNullOrWhiteSpace(fila.Codigo))
                fila.Advertencias.Add("Código se generará automáticamente");

            if (fila.FechaAdquisicion is null)
                fila.Advertencias.Add("Fecha de adquisición usa valor por defecto (hoy)");

            if (string.IsNullOrWhiteSpace(fila.EstadoFisico))
            {
                fila.EstadoFisico = "bueno";
            }
            else
            {
                var estadoNorm = Normalizar(fila.EstadoFisico);
                if (estadoNorm != "bueno" && estadoNorm != "regular" && estadoNorm != "malo")
                {
                    fila.Advertencias.Add($"Estado '{fila.EstadoFisico}' no reconocido, se usará 'bueno'");
                    fila.EstadoFisico = "bueno";
                }
            }

            if (!EsConsumo(fila))
            {
                if (string.IsNullOrWhiteSpace(fila.AulaNombre))
                    fila.Advertencias.Add("Sin ubicación asignada");

                if (string.IsNullOrWhiteSpace(fila.FuncionarioNombre))
                    fila.Advertencias.Add("Sin responsable asignado");
            }

            if (fila.Errores.Any())
                fila.Estado = "ERROR";
            else if (fila.Advertencias.Any())
                fila.Estado = "WARNING";
            else
                fila.Estado = "OK";
        }
    }

    // ═══════════════════════════════════════════════════════
    // 6. PERSISTIR BIENES REALES + RECHAZO INTELIGENTE
    //    ✨ CON MENSAJE FINAL HONESTO
    // ═══════════════════════════════════════════════════════
    public async Task<ResultadoImportacionDTO> PersistirBienesAsync(
        List<FilaImportacionDTO> filas,
        int importacionId,
        Action<int>? onProgreso = null)
    {
        var resultado = new ResultadoImportacionDTO
        {
            ImportacionId = importacionId,
            TotalFilas = filas.Count
        };

        var filasParaImportar = filas
            .Where(f => f.Estado == "OK" || f.Estado == "WARNING")
            .ToList();

        // Pre-cargar catálogos en memoria (4 queries)
        var categorias = await CargarCategoriasAsync();
        var aulas = await CargarAulasAsync();
        var funcionarios = await CargarFuncionariosAsync();
        var vidasUtiles = await CargarVidasUtilesPorCgnAsync();

        int importadas = 0;
        int conError = 0;
        int conWarning = 0;
        int yaExistian = 0;   // ✨ Contador de bienes que ya existían (duplicados idénticos)

        foreach (var fila in filasParaImportar)
        {
            try
            {
                // ── 1. Resolver Categoría por nombre ──
                var categoria = BuscarPorNombre(categorias, fila.CategoriaNombre);
                if (categoria is null)
                {
                    fila.Errores.Add($"Categoría '{fila.CategoriaNombre}' no encontrada en el sistema");
                    fila.Estado = "ERROR";
                    await _impRepo.MarcarDetalleErrorPorFilaAsync(
                        importacionId, fila.NumeroFila, string.Join("; ", fila.Errores));
                    conError++;
                    onProgreso?.Invoke(importadas + conError);
                    continue;
                }

                // ── 2. Resolver Aula por nombre (opcional) ──
                int? aulaId = null;
                if (!string.IsNullOrWhiteSpace(fila.AulaNombre))
                {
                    var aula = BuscarPorNombre(aulas, fila.AulaNombre);
                    aulaId = aula?.Id;
                }

                // ── 3. Resolver Funcionario por nombre (opcional) ──
                int? funcionarioId = null;
                if (!string.IsNullOrWhiteSpace(fila.FuncionarioNombre))
                {
                    var func = BuscarPorNombre(funcionarios, fila.FuncionarioNombre);
                    funcionarioId = func?.Id;
                }

                // ── 4. Resolver VidaÚtil desde el CGN de la categoría ──
                int? vidaUtilId = null;
                if (!string.IsNullOrWhiteSpace(categoria.CodigoCgn))
                {
                    vidaUtilId = vidasUtiles.GetValueOrDefault(categoria.CodigoCgn);
                }

                // ── 5. Construir el Bien ──
                var tipoBienFinal = string.IsNullOrWhiteSpace(categoria.TipoBien)
                    ? (fila.TipoBien ?? "devolutivo").ToLower()
                    : categoria.TipoBien!.ToLower();

                if (tipoBienFinal == "ambos")
                    tipoBienFinal = "devolutivo";

                var bien = new Bien
                {
                    Codigo = fila.Codigo?.Trim() ?? "",
                    Nombre = fila.Nombre!.Trim(),
                    Descripcion = fila.Observaciones,
                    CategoriaId = categoria.Id,
                    TipoBien = tipoBienFinal,
                    Marca = fila.Marca,
                    Modelo = fila.Modelo,
                    Serie = fila.Serie,
                    ValorAdquisicion = fila.ValorAdquisicion ?? 0,
                    FechaAdquisicion = fila.FechaAdquisicion ?? DateTime.Today,
                    EstadoFisico = fila.EstadoFisico ?? "bueno",
                    Activo = true,
                    AulaId = aulaId,
                    FuncionarioId = funcionarioId,
                    VidaUtilId = vidaUtilId,
                    ValorResidual = 0,
                    Cantidad = (int)(fila.StockInicial ?? 1)
                };

                // ═══════════════════════════════════════════════════════
                // ✨ 5.5. VALIDACIÓN DE DUPLICADOS (RECHAZO INTELIGENTE)
                // ═══════════════════════════════════════════════════════
                if (!string.IsNullOrWhiteSpace(bien.Codigo))
                {
                    var bienExistente = await _bienRepo.ObtenerPorCodigoAsync(bien.Codigo);

                    if (bienExistente is not null)
                    {
                        // Comparar si los datos son idénticos (idempotencia)
                        bool datosIdenticos =
                            string.Equals(bienExistente.Nombre?.Trim(), bien.Nombre?.Trim(),
                                StringComparison.OrdinalIgnoreCase) &&
                            bienExistente.ValorAdquisicion == bien.ValorAdquisicion &&
                            bienExistente.CategoriaId == bien.CategoriaId;

                        if (datosIdenticos)
                        {
                            // ✅ Idempotente: no crear duplicado, marcar como OK con advertencia
                            fila.Advertencias.Add($"'{bien.Codigo}' ya existía con datos idénticos — no se duplicó");
                            await _impRepo.MarcarDetalleImportadoPorFilaAsync(
                                importacionId, fila.NumeroFila, bienExistente.Id);
                            importadas++;
                            conWarning++;
                            yaExistian++;   // ✨ NUEVO — registra el duplicado idéntico
                            onProgreso?.Invoke(importadas + conError);
                            continue;
                        }
                        else
                        {
                            // ❌ Código existe con datos diferentes → RECHAZAR
                            var mensajeError =
                                $"El código '{bien.Codigo}' ya existe en el sistema con datos diferentes. " +
                                $"Bien existente: '{bienExistente.Nombre}' — Valor: {bienExistente.ValorAdquisicion:C0}";

                            fila.Errores.Add(mensajeError);
                            fila.Estado = "ERROR";
                            await _impRepo.MarcarDetalleErrorPorFilaAsync(
                                importacionId, fila.NumeroFila, mensajeError);
                            conError++;
                            onProgreso?.Invoke(importadas + conError);
                            continue;
                        }
                    }
                }

                // ── 6. Crear bien REAL ──
                var bienId = await _bienRepo.CrearAsync(bien);

                // ── 7. Marcar el detalle como importado ──
                await _impRepo.MarcarDetalleImportadoPorFilaAsync(
                    importacionId, fila.NumeroFila, bienId);

                importadas++;
                if (fila.Advertencias.Any()) conWarning++;
                onProgreso?.Invoke(importadas + conError);
            }
            catch (Exception ex)
            {
                fila.Errores.Add(ex.Message);
                fila.Estado = "ERROR";
                await _impRepo.MarcarDetalleErrorPorFilaAsync(
                    importacionId, fila.NumeroFila, ex.Message);
                conError++;
                onProgreso?.Invoke(importadas + conError);
            }
        }

        // Actualizar contadores reales de la cabecera
        await _impRepo.ActualizarContadoresAsync(
            importacionId, importadas, conWarning, conError);

        resultado.FilasImportadas = importadas;
        resultado.FilasConError = conError;
        resultado.FilasConWarning = conWarning;
        resultado.Exito = conError == 0;

        // ═══════════════════════════════════════════════════════════════
        // ✨ MENSAJE FINAL INTELIGENTE — Distingue nuevos vs existentes
        // ═══════════════════════════════════════════════════════════════
        var nuevasReales = importadas - yaExistian;

        if (conError > 0)
        {
            // Caso 1: Hubo errores
            resultado.Mensaje = $"Se procesaron {importadas} fila(s): " +
                                $"{nuevasReales} bien(es) nuevo(s) creado(s), " +
                                $"{yaExistian} ya existían (sin duplicar) y " +
                                $"{conError} fila(s) fallaron.";
        }
        else if (importadas > 0 && yaExistian == importadas)
        {
            // Caso 2: TODOS los bienes ya existían (típico de reimportar el mismo archivo)
            resultado.Mensaje = $"Los {importadas} bienes ya existían con datos idénticos. " +
                                "No se creó ningún duplicado.";
        }
        else if (yaExistian > 0)
        {
            // Caso 3: Mixto (algunos nuevos + algunos existentes)
            resultado.Mensaje = $"Se crearon {nuevasReales} bien(es) nuevo(s). " +
                                $"{yaExistian} ya existían y no se duplicaron.";
        }
        else if (importadas > 0)
        {
            // Caso 4: Todos nuevos (importación limpia)
            resultado.Mensaje = $"Se importaron {nuevasReales} bienes correctamente.";
        }
        else
        {
            // Caso 5: Nada se importó (0 filas válidas)
            resultado.Mensaje = "No se importó ningún bien. Verifique el archivo.";
        }

        return resultado;
    }

    // ═══════════════════════════════════════════════════════
    // HELPERS — Carga de catálogos
    // ═══════════════════════════════════════════════════════
    private async Task<Dictionary<string, Categoria>> CargarCategoriasAsync()
    {
        const string sql = @"
            SELECT id, nombre, descripcion,
                   codigo_cgn AS CodigoCgn,
                   tipo_bien AS TipoBien,
                   estado, fecha_creacion AS FechaCreacion
            FROM categorias
            WHERE estado = TRUE;";

        using var cn = new NpgsqlConnection(_cs);
        var lista = (await cn.QueryAsync<Categoria>(sql)).ToList();

        var dict = new Dictionary<string, Categoria>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in lista)
            dict[Normalizar(c.Nombre)] = c;
        return dict;
    }

    private async Task<Dictionary<string, (int Id, string Nombre)>> CargarAulasAsync()
    {
        const string sql = @"
            SELECT a.id AS Id, a.nombre AS Nombre
            FROM aulas a
            LEFT JOIN bloques bl ON bl.id = a.bloque_id
            LEFT JOIN sedes s ON s.id = bl.sede_id
            WHERE s.institucion_id = @InstitucionId
              AND a.activo = TRUE;";

        using var cn = new NpgsqlConnection(_cs);
        var lista = (await cn.QueryAsync<(int Id, string Nombre)>(sql,
            new { InstitucionId = _sesion.InstitucionId })).ToList();

        var dict = new Dictionary<string, (int Id, string Nombre)>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in lista)
            dict[Normalizar(a.Nombre)] = a;
        return dict;
    }

    private async Task<Dictionary<string, (int Id, string Nombre)>> CargarFuncionariosAsync()
    {
        const string sql = @"
            SELECT id AS Id, nombre_completo AS Nombre
            FROM funcionarios
            WHERE institucion_id = @InstitucionId
              AND activo = TRUE;";

        using var cn = new NpgsqlConnection(_cs);
        var lista = (await cn.QueryAsync<(int Id, string Nombre)>(sql,
            new { InstitucionId = _sesion.InstitucionId })).ToList();

        var dict = new Dictionary<string, (int Id, string Nombre)>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in lista)
            dict[Normalizar(f.Nombre)] = f;
        return dict;
    }

    private async Task<Dictionary<string, int>> CargarVidasUtilesPorCgnAsync()
    {
        const string sql = @"
            SELECT id AS Id, codigo_cgn AS CodigoCgn
            FROM catalogo_cgn
            WHERE activo = TRUE;";

        using var cn = new NpgsqlConnection(_cs);
        var lista = (await cn.QueryAsync<(int Id, string CodigoCgn)>(sql)).ToList();

        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in lista)
            dict[v.CodigoCgn] = v.Id;
        return dict;
    }

    private static Categoria? BuscarPorNombre(Dictionary<string, Categoria> dict, string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return null;
        return dict.TryGetValue(Normalizar(nombre), out var valor) ? valor : null;
    }

    private static (int Id, string Nombre)? BuscarPorNombre(
        Dictionary<string, (int Id, string Nombre)> dict, string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return null;
        return dict.TryGetValue(Normalizar(nombre), out var valor) ? valor : (null);
    }

    // ═══════════════════════════════════════════════════════
    // HELPERS — Utilidades
    // ═══════════════════════════════════════════════════════
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

    private static string Normalizar(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var sb = new StringBuilder();
        foreach (var c in s.ToLowerInvariant())
        {
            var norm = c switch
            {
                'á' or 'à' or 'ä' or 'â' => 'a',
                'é' or 'è' or 'ë' or 'ê' => 'e',
                'í' or 'ì' or 'ï' or 'î' => 'i',
                'ó' or 'ò' or 'ö' or 'ô' => 'o',
                'ú' or 'ù' or 'ü' or 'û' => 'u',
                'ñ' => 'n',
                ' ' or '-' or '_' => '_',
                _ => c
            };
            if (char.IsLetterOrDigit(norm) || norm == '_')
                sb.Append(norm);
        }
        return sb.ToString();
    }

    private static decimal? ParseDecimal(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;

        var limpio = valor.Replace("$", "").Replace(" ", "").Trim();

        if (limpio.Contains(',') && limpio.Contains('.'))
            limpio = limpio.Replace(",", "");
        else if (limpio.Contains(','))
            limpio = limpio.Replace(",", ".");

        if (decimal.TryParse(limpio, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            return result;

        return null;
    }

    private static DateTime? ParseFecha(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;

        var formatos = new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "MM/dd/yyyy" };

        foreach (var formato in formatos)
        {
            if (DateTime.TryParseExact(valor, formato, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var result))
                return result;
        }

        if (DateTime.TryParse(valor, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result2))
            return result2;

        return null;
    }

    private static bool EsConsumo(FilaImportacionDTO fila)
    {
        if (string.IsNullOrWhiteSpace(fila.TipoBien)) return false;
        var norm = Normalizar(fila.TipoBien);
        return norm.Contains("consumo") || norm.Contains("consumible");
    }

    private static List<string> SepararCsv(string linea, char separador)
    {
        var celdas = new List<string>();
        var actual = new StringBuilder();
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
}