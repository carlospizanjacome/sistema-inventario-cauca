namespace Almacen.Models;

public class Sede
{
    public int Id { get; set; }
    public int InstitucionId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string? Tipo { get; set; } = "Principal";
    public bool Activo { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    // Extendido (no mapeado directo)
    public string? InstitucionNombre { get; set; }
}