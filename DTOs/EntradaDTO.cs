using System.ComponentModel.DataAnnotations;

namespace Almacen.DTOs;

public class EntradaDTO
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El bien es obligatorio")]
    public int BienId { get; set; }

    [Required(ErrorMessage = "El tipo de fuente es obligatorio")]
    [StringLength(30)]
    public string TipoFuente { get; set; } = "FSE";

    [StringLength(50)]
    public string? NumeroFactura { get; set; }

    [Required(ErrorMessage = "La fecha de entrada es obligatoria")]
    public DateTime? FechaEntrada { get; set; } = DateTime.Today;

    public decimal Valor { get; set; }

    [StringLength(200)]
    public string? Proveedor { get; set; }

    public int? FuncionarioRecibeId { get; set; }

    public string? Observaciones { get; set; }

    // ── Navegación (read-only) ──
    public string? BienCodigo { get; set; }
    public string? BienNombre { get; set; }
    public string? FuncionarioNombre { get; set; }

    public int? ProveedorId { get; set; }
    public string? ProveedorNombre { get; set; }   // para mostrar en tabla

    public int InstitucionId { get; set; }
}