using Almacen.DTOs;
using Almacen.Models;
using ClosedXML.Excel;

namespace Almacen.Services;

/// <summary>
/// Servicio centralizado para exportar datos a Excel.
/// Sigue el formato de la Contaduría General de la Nación.
/// </summary>
public class ExcelExportService
{
    /// <summary>
    /// Exporta un inventario consolidado con formato CGN.
    /// </summary>
    public byte[] ExportarInventarioCGN(
        string nombreInstitucion,
        string nombreResponsable,
        IEnumerable<Bien> bienes)
    {
        using var workbook = new XLWorkbook();

        // ═══ HOJA 1 — INVENTARIO DETALLADO ═══
        var ws = workbook.Worksheets.Add("Inventario");

        // Encabezado institucional (filas 1-4)
        ws.Cell(1, 1).Value = "REPÚBLICA DE COLOMBIA";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 12;
        ws.Range(1, 1, 1, 10).Merge();

        ws.Cell(2, 1).Value = "CONTADURÍA GENERAL DE LA NACIÓN";
        ws.Cell(2, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Style.Font.FontSize = 10;
        ws.Range(2, 1, 2, 10).Merge();

        ws.Cell(3, 1).Value = $"INVENTARIO DE BIENES DEVOLUTIVOS — {nombreInstitucion}";
        ws.Cell(3, 1).Style.Font.Bold = true;
        ws.Cell(3, 1).Style.Font.FontSize = 11;
        ws.Cell(3, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1e3a8a");
        ws.Cell(3, 1).Style.Font.FontColor = XLColor.White;
        ws.Range(3, 1, 3, 10).Merge();
        ws.Row(3).Height = 22;

        ws.Cell(4, 1).Value = $"Fecha de corte: {DateTime.Now:dd/MM/yyyy}";
        ws.Cell(4, 1).Style.Font.Italic = true;
        ws.Range(4, 1, 4, 10).Merge();

        // Encabezados de tabla (fila 6)
        int filaHeader = 6;
        var headers = new[]
        {
            "Código CGN", "Código Bien", "Nombre del Bien", "Categoría",
            "Marca", "Modelo", "Serie", "Cantidad",
            "Valor Adquisición", "Fecha Adquisición", "Estado Físico",
            "Vida Útil (meses)", "Deprec. Acumulada", "Valor Neto",
            "Ubicación", "Responsable", "Activo"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(filaHeader, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#4f46e5");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        // Datos (desde fila 7)
        int fila = filaHeader + 1;
        foreach (var b in bienes)
        {
            ws.Cell(fila, 1).Value = "-";                       // Código CGN se llena manualmente según catálogo
            ws.Cell(fila, 2).Value = b.Codigo ?? "";
            ws.Cell(fila, 3).Value = b.Nombre ?? "";
            ws.Cell(fila, 4).Value = b.CategoriaNombre ?? "";
            ws.Cell(fila, 5).Value = b.Marca ?? "";
            ws.Cell(fila, 6).Value = b.Modelo ?? "";
            ws.Cell(fila, 7).Value = b.Serie ?? "";
            ws.Cell(fila, 8).Value = b.Cantidad;
            ws.Cell(fila, 9).Value = b.ValorAdquisicion;
            ws.Cell(fila, 9).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(fila, 10).Value = b.FechaAdquisicion?.ToString("dd/MM/yyyy") ?? "";
            ws.Cell(fila, 11).Value = b.EstadoFisico ?? "";
            ws.Cell(fila, 12).Value = 0;                         // Vida útil (se puede traer en query)
            ws.Cell(fila, 13).Value = b.DepreciacionAcumulada;
            ws.Cell(fila, 13).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(fila, 14).Value = b.ValorNeto;
            ws.Cell(fila, 14).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(fila, 15).Value = b.AulaNombre ?? b.Ubicacion ?? "";
            ws.Cell(fila, 16).Value = b.FuncionarioNombre ?? b.Responsable ?? "";
            ws.Cell(fila, 17).Value = b.Activo ? "Sí" : "No";

            fila++;
        }

        // Fila de totales
        int filaTotal = fila;
        ws.Cell(filaTotal, 8).Value = "TOTAL:";
        ws.Cell(filaTotal, 8).Style.Font.Bold = true;
        ws.Cell(filaTotal, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Cell(filaTotal, 9).FormulaA1 = $"SUM(I7:I{fila - 1})";
        ws.Cell(filaTotal, 9).Style.Font.Bold = true;
        ws.Cell(filaTotal, 9).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(filaTotal, 13).FormulaA1 = $"SUM(M7:M{fila - 1})";
        ws.Cell(filaTotal, 13).Style.Font.Bold = true;
        ws.Cell(filaTotal, 13).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(filaTotal, 14).FormulaA1 = $"SUM(N7:N{fila - 1})";
        ws.Cell(filaTotal, 14).Style.Font.Bold = true;
        ws.Cell(filaTotal, 14).Style.NumberFormat.Format = "#,##0.00";

        ws.Range(filaTotal, 1, filaTotal, 17).Style.Fill.BackgroundColor = XLColor.FromHtml("#e2e8f0");

        // ═══ HOJA 2 — RESUMEN POR CATEGORÍA ═══
        var wsResumen = workbook.Worksheets.Add("Resumen");

        wsResumen.Cell(1, 1).Value = "RESUMEN POR CATEGORÍA";
        wsResumen.Cell(1, 1).Style.Font.Bold = true;
        wsResumen.Cell(1, 1).Style.Font.FontSize = 12;
        wsResumen.Range(1, 1, 1, 4).Merge();

        wsResumen.Cell(3, 1).Value = "Categoría";
        wsResumen.Cell(3, 2).Value = "Cantidad Bienes";
        wsResumen.Cell(3, 3).Value = "Valor Adquisición";
        wsResumen.Cell(3, 4).Value = "Valor Neto";

        var headerRange = wsResumen.Range(3, 1, 3, 4);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#4f46e5");
        headerRange.Style.Font.FontColor = XLColor.White;

        var porCategoria = bienes
            .GroupBy(b => b.CategoriaNombre ?? "Sin categoría")
            .Select(g => new
            {
                Categoria = g.Key,
                Cantidad = g.Count(),
                Valor = g.Sum(x => x.ValorAdquisicion),
                ValorNeto = g.Sum(x => x.ValorNeto)
            })
            .OrderByDescending(x => x.Valor);

        int filaCat = 4;
        foreach (var c in porCategoria)
        {
            wsResumen.Cell(filaCat, 1).Value = c.Categoria;
            wsResumen.Cell(filaCat, 2).Value = c.Cantidad;
            wsResumen.Cell(filaCat, 3).Value = c.Valor;
            wsResumen.Cell(filaCat, 3).Style.NumberFormat.Format = "#,##0.00";
            wsResumen.Cell(filaCat, 4).Value = c.ValorNeto;
            wsResumen.Cell(filaCat, 4).Style.NumberFormat.Format = "#,##0.00";
            filaCat++;
        }

        // ═══ HOJA 3 — INFORMACIÓN DEL REPORTE ═══
        var wsInfo = workbook.Worksheets.Add("Info");

        wsInfo.Cell(1, 1).Value = "INFORMACIÓN DEL REPORTE";
        wsInfo.Cell(1, 1).Style.Font.Bold = true;
        wsInfo.Cell(1, 1).Style.Font.FontSize = 12;

        wsInfo.Cell(3, 1).Value = "Institución:";
        wsInfo.Cell(3, 2).Value = nombreInstitucion;
        wsInfo.Cell(4, 1).Value = "Responsable:";
        wsInfo.Cell(4, 2).Value = nombreResponsable;
        wsInfo.Cell(5, 1).Value = "Fecha de generación:";
        wsInfo.Cell(5, 2).Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        wsInfo.Cell(6, 1).Value = "Total bienes:";
        wsInfo.Cell(6, 2).Value = bienes.Count();
        wsInfo.Cell(7, 1).Value = "Valor total:";
        wsInfo.Cell(7, 2).Value = bienes.Sum(b => b.ValorAdquisicion);
        wsInfo.Cell(7, 2).Style.NumberFormat.Format = "#,##0.00";

        // Ajustar ancho de columnas
        ws.Columns().AdjustToContents();
        wsResumen.Columns().AdjustToContents();

        // Convertir a bytes
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}