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

    [Required(ErrorMessage = "El valor de adquisición es obligatorio")]
    [Range(1, double.MaxValue, ErrorMessage = "El valor debe ser mayor a 0")]
    public decimal ValorAdquisicion { get; set; }

    public DateTime? FechaAdquisicion { get; set; }

    [StringLength(20)]
    public string? EstadoFisico { get; set; } = "Bueno";

    public int? InstitucionId { get; set; }

    [Required(ErrorMessage = "La sede es obligatoria")]
    public int? SedeId { get; set; }

    [Required(ErrorMessage = "Debe seleccionar un Aula (Ubicación)")]
    public int? AulaId { get; set; }

    [Required(ErrorMessage = "Debe seleccionar un Responsable")]
    public int? FuncionarioId { get; set; }

    public int? VidaUtilId { get; set; }
    public string? CodigoQr { get; set; }
    public decimal ValorResidual { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser al menos 1")]
    public int Cantidad { get; set; } = 1;

    public string? Ubicacion { get; set; }
    public string? Responsable { get; set; }

    public bool Activo { get; set; } = true;

    public string? CategoriaNombre { get; set; }
    public string? AulaNombre { get; set; }
    public string? BloqueNombre { get; set; }
    public string? SedeNombre { get; set; }
    public string? FuncionarioNombre { get; set; }
}