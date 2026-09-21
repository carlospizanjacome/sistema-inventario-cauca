namespace Almacen.Models;

public class Funcionario
{
    public int Id { get; set; }
    public string Cedula { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string? Cargo { get; set; }
    public string? TipoVinculacion { get; set; }
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public int? SedeId { get; set; }
    public int? UsuarioId { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public string? SedeNombre { get; set; }
    public string? InstitucionNombre { get; set; }
    public string? UsuarioEmail { get; set; }
}