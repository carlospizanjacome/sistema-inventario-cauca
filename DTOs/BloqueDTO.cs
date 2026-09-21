using System.ComponentModel.DataAnnotations;

namespace Almacen.DTOs;

public class BloqueDTO
{
    public int Id { get; set; }

    [Required(ErrorMessage = "La sede es obligatoria")]
    public int SedeId { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Descripcion { get; set; }

    [Range(1, 20, ErrorMessage = "Pisos entre 1 y 20")]
    public int? Pisos { get; set; }

    public bool Activo { get; set; } = true;

    public string? SedeNombre { get; set; }
    public string? InstitucionNombre { get; set; }
}