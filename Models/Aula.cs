namespace Almacen.Models;

public class Aula
{
    public int Id { get; set; }
    public int BloqueId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Tipo { get; set; } = "Aula";
    public int? Capacidad { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public string? BloqueNombre { get; set; }
    public string? SedeNombre { get; set; }
    public string? InstitucionNombre { get; set; }
}