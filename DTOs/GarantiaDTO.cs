using System.ComponentModel.DataAnnotations;

namespace Almacen.DTOs;

public class GarantiaDTO
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Debe seleccionar un bien")]
    public int BienId { get; set; }

    public int? ProveedorId { get; set; }

    [StringLength(100)]
    public string? NumeroGarantia { get; set; }

    public string Tipo { get; set; } = "FABRICA";

    [Required(ErrorMessage = "La fecha de inicio es obligatoria")]
    public DateTime FechaInicio { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "La fecha de vencimiento es obligatoria")]
    public DateTime FechaVencimiento { get; set; } = DateTime.Today.AddYears(1);

    public string? Condiciones { get; set; }
    public string? ContactoProveedor { get; set; }
    public string? Observaciones { get; set; }

    // ── Navegación ──
    public string? BienCodigo { get; set; }
    public string? BienNombre { get; set; }
    public string? BienCategoriaNombre { get; set; }
    public string? ProveedorNombre { get; set; }
    public string? ProveedorNit { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // ── Calculadas ──
    public int DiasRestantes => (FechaVencimiento.Date - DateTime.Today).Days;
    public bool EstaVencida => FechaVencimiento.Date < DateTime.Today;
    public bool PorVencer => !EstaVencida && DiasRestantes <= 30;
    public string EstadoCalculado => EstaVencida ? "VENCIDA" : (PorVencer ? "POR_VENCER" : "VIGENTE");
}