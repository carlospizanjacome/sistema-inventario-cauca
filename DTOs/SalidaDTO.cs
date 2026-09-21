using System.ComponentModel.DataAnnotations;

namespace Almacen.DTOs;

public class SalidaDTO
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El bien es obligatorio")]
    public int BienId { get; set; }

    [Required(ErrorMessage = "El tipo de baja es obligatorio")]
    [StringLength(30)]
    public string TipoBaja { get; set; } = "Obsolescencia";

    [Required(ErrorMessage = "El motivo es obligatorio")]
    public string Motivo { get; set; } = string.Empty;

    [Required(ErrorMessage = "La fecha es obligatoria")]
    public DateTime? FechaSalida { get; set; } = DateTime.Today;

    [StringLength(50)]
    public string? NumeroActaComite { get; set; }

    [StringLength(50)]
    public string? NumeroDenuncia { get; set; }

    public decimal ValorSalida { get; set; }

    public int? FuncionarioApruebaId { get; set; }

    public string? Observaciones { get; set; }

    public int InstitucionId { get; set; }
    // ── Navegación (read-only) ──
    public string? BienCodigo { get; set; }
    public string? BienNombre { get; set; }
    public string? FuncionarioNombre { get; set; }
   
}