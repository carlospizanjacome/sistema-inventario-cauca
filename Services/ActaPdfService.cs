using Almacen.DTOs;
using Almacen.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Almacen.Services;

/// <summary>
/// Genera el "Acta de Responsabilidad Individual" para firmar
/// cuando un funcionario recibe custodia de uno o más bienes.
/// </summary>
public class ActaPdfService
{
    public byte[] GenerarActaResponsabilidad(
        string nombreInstitucion,
        string nitInstitucion,
        FuncionarioDTO funcionario,
        IEnumerable<Bien> bienes,
        string nombreRector,
        string ciudad = "Popayán")
    {
        var listaBienes = bienes.ToList();
        var totalValor = listaBienes.Sum(b => b.ValorAdquisicion);
        var fechaActual = DateTime.Now;
        var numeroActa = $"AR-{fechaActual:yyyyMMdd}-{funcionario.Id:D4}";

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Lato"));

                // ═══════════ HEADER ═══════════
                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text("REPÚBLICA DE COLOMBIA")
                        .FontSize(11).SemiBold();
                    col.Item().AlignCenter().Text("SECRETARÍA DE EDUCACIÓN DEL CAUCA")
                        .FontSize(10);
                    col.Item().AlignCenter().Text(nombreInstitucion.ToUpper())
                        .FontSize(11).Bold();
                    if (!string.IsNullOrWhiteSpace(nitInstitucion))
                    {
                        col.Item().AlignCenter().Text($"NIT: {nitInstitucion}")
                            .FontSize(9);
                    }

                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Medium);

                    col.Item().AlignCenter().Text("ACTA DE RESPONSABILIDAD INDIVIDUAL")
                        .FontSize(14).Bold();

                    col.Item().AlignRight().Text($"Acta N°: {numeroActa}")
                        .FontSize(9);
                });

                // ═══════════ CONTENT ═══════════
                page.Content().PaddingVertical(15).Column(col =>
                {
                    col.Spacing(10);

                    // Párrafo intro
                    col.Item().Text(text =>
                    {
                        text.Span("En la ciudad de ").FontSize(10);
                        text.Span(ciudad).FontSize(10).SemiBold();
                        text.Span($", a los {fechaActual.Day} días del mes de {ObtenerMesEspanol(fechaActual.Month)} de {fechaActual.Year}, ").FontSize(10);
                        text.Span("se hace entrega de los bienes que a continuación se relacionan, bajo la modalidad de custodia y responsabilidad individual:").FontSize(10);
                    });

                    // Datos del funcionario (cuentandante)
                    col.Item().PaddingTop(5).Text("DATOS DEL CUENTANDANTE").FontSize(11).Bold();

                    col.Item().Border(1).BorderColor(Colors.Grey.Medium).Padding(8).Column(c =>
                    {
                        c.Item().Row(row =>
                        {
                            row.RelativeItem().Text(t =>
                            {
                                t.Span("Nombre completo: ").SemiBold();
                                t.Span(funcionario.NombreCompleto);
                            });
                        });
                        c.Item().Row(row =>
                        {
                            row.RelativeItem().Text(t =>
                            {
                                t.Span("Cédula: ").SemiBold();
                                t.Span(funcionario.Cedula);
                            });
                            row.RelativeItem().Text(t =>
                            {
                                t.Span("Cargo: ").SemiBold();
                                t.Span(funcionario.Cargo ?? "—");
                            });
                        });
                        c.Item().Row(row =>
                        {
                            row.RelativeItem().Text(t =>
                            {
                                t.Span("Tipo vinculación: ").SemiBold();
                                t.Span(funcionario.TipoVinculacion ?? "—");
                            });
                            row.RelativeItem().Text(t =>
                            {
                                t.Span("Sede: ").SemiBold();
                                t.Span(funcionario.SedeNombre ?? "—");
                            });
                        });
                    });

                    // Tabla de bienes
                    col.Item().PaddingTop(10).Text("BIENES ASIGNADOS").FontSize(11).Bold();

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(55);   // Código
                            columns.RelativeColumn(3);    // Nombre
                            columns.RelativeColumn(1);    // Marca
                            columns.RelativeColumn(1);    // Serie
                            columns.ConstantColumn(35);   // Cant.
                            columns.ConstantColumn(70);   // Valor
                        });

                        // Header
                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("Código").FontSize(8).Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("Nombre").FontSize(8).Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("Marca").FontSize(8).Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("Serie").FontSize(8).Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("Cant.").FontSize(8).Bold().AlignCenter();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("Valor").FontSize(8).Bold().AlignRight();
                        });

                        // Filas
                        foreach (var b in listaBienes)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(b.Codigo ?? "—").FontSize(8);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(b.Nombre ?? "—").FontSize(8);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(b.Marca ?? "—").FontSize(8);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(b.Serie ?? "—").FontSize(8);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(b.Cantidad.ToString()).FontSize(8).AlignCenter();
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(b.ValorAdquisicion.ToString("N0")).FontSize(8).AlignRight();
                        }

                        // Fila de total
                        table.Cell().ColumnSpan(5).Padding(4).Text("TOTAL:").FontSize(9).Bold().AlignRight();
                        table.Cell().Padding(4).Text(totalValor.ToString("N0")).FontSize(9).Bold().AlignRight();
                    });

                    // Compromiso
                    col.Item().PaddingTop(15).Text("COMPROMISO").FontSize(11).Bold();

                    col.Item().Text(
                        "El cuentandante se compromete a: (1) Custodiar los bienes descritos con la debida diligencia; " +
                        "(2) Responder por su conservación, uso adecuado y destinación exclusiva al servicio de la institución; " +
                        "(3) Informar oportunamente cualquier daño, pérdida o novedad a la rectoría; " +
                        "(4) Devolver los bienes en las mismas condiciones en que los recibe, salvo el deterioro natural por uso.")
                        .FontSize(9).Justify();

                    // Firmas
                    col.Item().PaddingTop(60).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().AlignCenter().Text("_________________________________").FontSize(10);
                            c.Item().AlignCenter().Text(funcionario.NombreCompleto).FontSize(9).SemiBold();
                            c.Item().AlignCenter().Text($"C.C. {funcionario.Cedula}").FontSize(8);
                            c.Item().AlignCenter().Text("Cuentandante").FontSize(8).Italic();
                        });

                        row.RelativeItem().Column(c =>
                        {
                            c.Item().AlignCenter().Text("_________________________________").FontSize(10);
                            c.Item().AlignCenter().Text(nombreRector).FontSize(9).SemiBold();
                            c.Item().AlignCenter().Text("Rector(a)").FontSize(8).Italic();
                        });
                    });
                });

                // ═══════════ FOOTER ═══════════
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span($"Acta generada el {fechaActual:dd/MM/yyyy HH:mm} — ").FontSize(7).FontColor(Colors.Grey.Medium);
                    text.Span("Sistema de Inventario — Almacén Municipal").FontSize(7).FontColor(Colors.Grey.Medium);
                });
            });
        }).GeneratePdf();
    }

    private static string ObtenerMesEspanol(int mes) => mes switch
    {
        1 => "enero",
        2 => "febrero",
        3 => "marzo",
        4 => "abril",
        5 => "mayo",
        6 => "junio",
        7 => "julio",
        8 => "agosto",
        9 => "septiembre",
        10 => "octubre",
        11 => "noviembre",
        12 => "diciembre",
        _ => ""
    };
}