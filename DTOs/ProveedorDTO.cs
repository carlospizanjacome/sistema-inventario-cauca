using System.ComponentModel.DataAnnotations;

namespace Almacen.DTOs;

public class ProveedorDTO
{
    public int Id { get; set; }

    [StringLength(30)]
    public string? Nit { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(200)]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Direccion { get; set; }

    [StringLength(30)]
    public string? Telefono { get; set; }

    [StringLength(150)]
    [EmailAddress(ErrorMessage = "Email inválido")]
    public string? Email { get; set; }

    [StringLength(150)]
    public string? Contacto { get; set; }

    public string? Observaciones { get; set; }

    public bool Activo { get; set; } = true;
}