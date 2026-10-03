namespace Almacen.Models;

public class Importacion
{
    public int Id { get; set; }
    public int InstitucionId { get; set; }
    public int UsuarioId { get; set; }
    public string NombreArchivo { get; set; } = string.Empty;
    public string TipoArchivo { get; set; } = string.Empty;

    public int TotalFilas { get; set; }
    public int FilasExitosas { get; set; }
    public int FilasConWarning { get; set; }
    public int FilasConError { get; set; }
    public int FilasOmitidas { get; set; }

    public string Estado { get; set; } = "EN_PROCESO";
    public string? MapeoColumnas { get; set; }
    public string? Observaciones { get; set; }

    public DateTime FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public DateTime CreatedAt { get; set; }

    // Soft-delete
    public bool Anulada { get; set; }
    public int? AnuladaPor { get; set; }
    public DateTime? AnuladaFecha { get; set; }
    public string? AnuladaMotivo { get; set; }
}

public class ImportacionDetalle
{
    public int Id { get; set; }
    public int ImportacionId { get; set; }
    public int NumeroFila { get; set; }
    public string DatosCrudos { get; set; } = string.Empty;
    public string Estado { get; set; } = "PENDIENTE";
    public string? Errores { get; set; }
    public string? Advertencias { get; set; }
    public int? BienId { get; set; }
    public DateTime CreatedAt { get; set; }
}