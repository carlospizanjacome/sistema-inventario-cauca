using System.ComponentModel.DataAnnotations;

namespace Almacen.DTOs;

public class ReporteNormativoDTO
{
    public int Id { get; set; }

    [Required(ErrorMessage = "La entidad receptora es obligatoria")]
    [StringLength(150)]
    public string EntidadReceptora { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre del reporte es obligatorio")]
    [StringLength(200)]
    public string NombreReporte { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    [StringLength(200)]
    public string? SujetoObligado { get; set; }

    [Required]
    public string Periodicidad { get; set; } = "ANUAL";

    [StringLength(80)]
    public string? FechaLimite { get; set; }

    public DateTime? VigenciaDesde { get; set; }
    public DateTime? VigenciaHasta { get; set; }

    [StringLength(30)]
    public string? VersionFormato { get; set; }

    [Required]
    public string FormatoSalida { get; set; } = "XLSX";

    public string? CamposRequeridos { get; set; }
    public string? ReglasValidacion { get; set; }

    [StringLength(300)]
    public string? FuenteOficial { get; set; }

    [StringLength(500)]
    public string? UrlOficial { get; set; }

    [StringLength(200)]
    public string? ResponsableInterno { get; set; }

    public string Estado { get; set; } = "BORRADOR";

    public string? PlantillaRuta { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Métricas
    public bool TienePlantilla => !string.IsNullOrWhiteSpace(PlantillaRuta);
    public bool EsVigente => Estado == "VIGENTE";
}