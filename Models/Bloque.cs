namespace Almacen.Models;

public class Bloque
{
    public int Id { get; set; }
    public int SedeId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int? Pisos { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public string? SedeNombre { get; set; }
    public string? InstitucionNombre { get; set; }
}