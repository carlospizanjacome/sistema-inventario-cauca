using System.ComponentModel.DataAnnotations;

namespace Almacen.DTOs;

public class UsuarioDTO
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(150)]
    public string NombreCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "El email es obligatorio")]
    [EmailAddress(ErrorMessage = "Email inválido")]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [StringLength(255, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres")]
    public string? Password { get; set; }

    public bool Activo { get; set; } = true;

    public int? RolId { get; set; }

    // ⬇️ NUEVAS
    public int InstitucionId { get; set; }
    public bool EsSuperAdmin { get; set; }

    // Navegación
    public string? NombreRol { get; set; }
    public string? InstitucionNombre { get; set; }
}