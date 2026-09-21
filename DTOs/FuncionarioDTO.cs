using System.ComponentModel.DataAnnotations;

namespace Almacen.DTOs;

public class FuncionarioDTO
{
    public int Id { get; set; }

    [Required(ErrorMessage = "La cédula es obligatoria")]
    [StringLength(20)]
    public string Cedula { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre completo es obligatorio")]
    [StringLength(200)]
    public string NombreCompleto { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Cargo { get; set; }

    [StringLength(50)]
    public string? TipoVinculacion { get; set; }

    [StringLength(150)]
    [EmailAddress(ErrorMessage = "Email inválido")]
    public string? Email { get; set; }

    [StringLength(30)]
    public string? Telefono { get; set; }

    public int? SedeId { get; set; }
    public int? UsuarioId { get; set; }
    public bool Activo { get; set; } = true;

    public string? SedeNombre { get; set; }

    public int InstitucionId { get; set; }
    public string? InstitucionNombre { get; set; }
    public string? UsuarioEmail { get; set; }

    
}