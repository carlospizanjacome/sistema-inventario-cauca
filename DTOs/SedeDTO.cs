using System.ComponentModel.DataAnnotations;

namespace Almacen.DTOs;

public class SedeDTO
{
    public int Id { get; set; }

    [Required(ErrorMessage = "La institución es obligatoria")]
    public int InstitucionId { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(150)]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Direccion { get; set; }

    [StringLength(30)]
    public string? Telefono { get; set; }

    [StringLength(30)]
    public string? Tipo { get; set; } = "Principal";

    public bool Activo { get; set; } = true;

    public string? InstitucionNombre { get; set; }
}