using System.ComponentModel.DataAnnotations;

namespace Almacen.DTOs;

public class PrestamoDTO
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Debe seleccionar un bien")]
    public int BienId { get; set; }

    [Required(ErrorMessage = "Debe seleccionar el funcionario que solicita")]
    public int FuncionarioSolicitaId { get; set; }

    [Required(ErrorMessage = "La fecha de devolución prevista es obligatoria")]
    public DateTime FechaDevolucionPrevista { get; set; } = DateTime.Today.AddDays(7);

    [Required(ErrorMessage = "El motivo es obligatorio")]
    [StringLength(500)]
    public string Motivo { get; set; } = string.Empty;

    public string? Observaciones { get; set; }

    // ── Navegación (read-only) ──
    public string? BienCodigo { get; set; }
    public string? BienNombre { get; set; }
    public string? BienCategoriaNombre { get; set; }
    public string? FuncionarioSolicitaNombre { get; set; }
    public string? FuncionarioSolicitaCedula { get; set; }
    public string? FuncionarioApruebaNombre { get; set; }

    public int InstitucionId { get; set; }

    // ── Estado y fechas ──
    public string Estado { get; set; } = "SOLICITADO";
    public DateTime FechaSolicitud { get; set; }
    public DateTime? FechaAprobacion { get; set; }
    public DateTime? FechaPrestamo { get; set; }
    public DateTime? FechaDevolucionReal { get; set; }
    public string? EstadoBienEntrega { get; set; }
    public string? EstadoBienDevolucion { get; set; }
    public string? ObservacionesEntrega { get; set; }
    public string? ObservacionesDevolucion { get; set; }

    // ── Métricas calculadas ──
    public int DiasPrestamo => Math.Max(0, (FechaDevolucionPrevista.Date - FechaSolicitud.Date).Days);
    public int? DiasRestantes => FechaDevolucionReal.HasValue
        ? null
        : (int?)(FechaDevolucionPrevista.Date - DateTime.Today).Days;
    public bool EstaVencido => Estado == "PRESTADO" && FechaDevolucionPrevista.Date < DateTime.Today;
}