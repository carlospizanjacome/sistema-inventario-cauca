using System.ComponentModel.DataAnnotations;

namespace Almacen.DTOs;

public class MantenimientoDTO
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Debe seleccionar un bien")]
    public int BienId { get; set; }

    [Required]
    public string Tipo { get; set; } = "CORRECTIVO";

    public string Estado { get; set; } = "ABIERTO";

    [Required(ErrorMessage = "La fecha de ingreso es obligatoria")]
    public DateTime FechaIngreso { get; set; } = DateTime.Today;

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

    // ── Navegación (read-only) ──
    public string? BienCodigo { get; set; }
    public string? BienNombre { get; set; }
    public string? BienCategoriaNombre { get; set; }
    public string? ProveedorNombre { get; set; }
    public string? ProveedorNit { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // ── Calculadas ──
    public int DiasFuera => FechaSalida.HasValue
        ? (FechaSalida.Value.Date - FechaIngreso.Date).Days
        : (DateTime.Today - FechaIngreso.Date).Days;

    public bool EstaVencido => Estado == "ABIERTO"
        && FechaDevolucionPrevista.HasValue
        && FechaDevolucionPrevista.Value.Date < DateTime.Today;
}