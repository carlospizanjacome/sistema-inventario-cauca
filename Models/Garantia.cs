namespace Almacen.Models;

public class Garantia
{
    public int Id { get; set; }
    public int BienId { get; set; }
    public int InstitucionId { get; set; }
    public int? ProveedorId { get; set; }

    public string? NumeroGarantia { get; set; }
    public string Tipo { get; set; } = "FABRICA";

    public DateTime FechaInicio { get; set; }
    public DateTime FechaVencimiento { get; set; }

    public string? Condiciones { get; set; }
    public string? ContactoProveedor { get; set; }
    public string? Observaciones { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}