using System.ComponentModel.DataAnnotations;

namespace Almacen.DTOs;

public class InstitucionDTO
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(200)]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Nit { get; set; }

    [StringLength(20)]
    public string? CodigoDane { get; set; }

    [StringLength(200)]
    public string? Direccion { get; set; }

    [StringLength(30)]
    public string? Telefono { get; set; }

    [StringLength(150)]
    [EmailAddress(ErrorMessage = "Email inválido")]
    public string? Email { get; set; }

    [StringLength(150)]
    public string? Rector { get; set; }

    public bool Activo { get; set; } = true;
}