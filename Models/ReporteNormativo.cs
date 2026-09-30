namespace Almacen.Models;

public class ReporteNormativo
{
    public int Id { get; set; }
    public string EntidadReceptora { get; set; } = string.Empty;
    public string NombreReporte { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? SujetoObligado { get; set; }
    public string Periodicidad { get; set; } = "ANUAL";
    public string? FechaLimite { get; set; }
    public DateTime? VigenciaDesde { get; set; }
    public DateTime? VigenciaHasta { get; set; }
    public string? VersionFormato { get; set; }
    public string FormatoSalida { get; set; } = "XLSX";
    public string? CamposRequeridos { get; set; }
    public string? ReglasValidacion { get; set; }
    public string? FuenteOficial { get; set; }
    public string? UrlOficial { get; set; }
    public string? ResponsableInterno { get; set; }
    public string Estado { get; set; } = "BORRADOR";
    public string? PlantillaRuta { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}