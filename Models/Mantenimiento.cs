namespace Almacen.Models;

public class Mantenimiento
{
    public int Id { get; set; }
    public int BienId { get; set; }
    public int InstitucionId { get; set; }

    public string Tipo { get; set; } = "CORRECTIVO";
    public string Estado { get; set; } = "ABIERTO";

    public DateTime FechaIngreso { get; set; }
    public DateTime? FechaSalida { get; set; }
    public DateTime? FechaDevolucionPrevista { get; set; }

    public int? ProveedorId { get; set; }
    public string? TecnicoResponsable { get; set; }

    public string? Diagnostico { get; set; }
    public string? TrabajoRealizado { get; set; }
    public string? RepuestosUtilizados { get; set; }
    public decimal Costo { get; set; }

    public string? EstadoBienIngreso { get; set; }
    public string? EstadoBienEgreso { get; set; }
    public string? Observaciones { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}