using System.ComponentModel.DataAnnotations;

namespace Almacen.DTOs;

public class TrasladoDTO
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El bien es obligatorio")]
    public int BienId { get; set; }

    public int? AulaOrigenId { get; set; }

    [Required(ErrorMessage = "El aula destino es obligatoria")]
    public int AulaDestinoId { get; set; }

    public int? FuncionarioAnteriorId { get; set; }

    public int? FuncionarioNuevoId { get; set; }

    [Required(ErrorMessage = "La fecha es obligatoria")]
    public DateTime? FechaTraslado { get; set; } = DateTime.Today;

    public string? Motivo { get; set; }

    public int InstitucionId { get; set; }

    // ── Navegación (read-only) ──
    public string? BienCodigo { get; set; }
    public string? BienNombre { get; set; }
    public string? AulaOrigenNombre { get; set; }
    public string? AulaDestinoNombre { get; set; }
    public string? FuncionarioAnteriorNombre { get; set; }
    public string? FuncionarioNuevoNombre { get; set; }

    
}