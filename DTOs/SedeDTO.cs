namespace Almacen.DTOs;

public class SedeDTO
{
    public int Id { get; set; }
    public int InstitucionId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string? Tipo { get; set; }
    public bool Activo { get; set; }

    // ✅ Coordenadas geográficas para el mapa
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }

    public DateTime CreatedAt { get; set; }
    public string? InstitucionNombre { get; set; }
}