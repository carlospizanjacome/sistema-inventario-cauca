using System.ComponentModel.DataAnnotations;

namespace Almacen.DTOs;

public class BienDTO
{
    public int Id { get; set; }

    [StringLength(30)]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(150)]
    public string Nombre { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    [Required(ErrorMessage = "La categoría es obligatoria")]
    public int? CategoriaId { get; set; }

    [Required(ErrorMessage = "El tipo es obligatorio")]
    [StringLength(20)]
    public string TipoBien { get; set; } = "devolutivo";

    [StringLength(80)]
    public string? Marca { get; set; }

    [StringLength(80)]
    public string? Modelo { get; set; }

    [StringLength(80)]
    public string? Serie { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "El valor debe ser mayor o igual a 0")]
    public decimal ValorAdquisicion { get; set; }

    public DateTime? FechaAdquisicion { get; set; }

    [StringLength(20)]
    public string? EstadoFisico { get; set; } = "Bueno";

    // ── FKs a la jerarquía ──
    public int? InstitucionId { get; set; }

    // ⬇️ Sede — OBLIGATORIA
    [Required(ErrorMessage = "La sede es obligatoria")]
    public int? SedeId { get; set; }

    public int? AulaId { get; set; }
    public int? FuncionarioId { get; set; }
    public int? VidaUtilId { get; set; }
    public string? CodigoQr { get; set; }
    public decimal ValorResidual { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser al menos 1")]
    public int Cantidad { get; set; } = 1;

    // ── LEGACY ──
    public string? Ubicacion { get; set; }
    public string? Responsable { get; set; }

    public bool Activo { get; set; } = true;

    // ── Navegación ──
    public string? CategoriaNombre { get; set; }
    public string? AulaNombre { get; set; }
    public string? BloqueNombre { get; set; }
    public string? SedeNombre { get; set; }
    public string? FuncionarioNombre { get; set; }
}