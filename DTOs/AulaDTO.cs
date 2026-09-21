using System.ComponentModel.DataAnnotations;

namespace Almacen.DTOs;

public class AulaDTO
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El bloque es obligatorio")]
    public int BloqueId { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Tipo { get; set; } = "Aula";

    [Range(1, 500, ErrorMessage = "Capacidad entre 1 y 500")]
    public int? Capacidad { get; set; }

    public bool Activo { get; set; } = true;

    public string? BloqueNombre { get; set; }
    public string? SedeNombre { get; set; }
    public string? InstitucionNombre { get; set; }
}