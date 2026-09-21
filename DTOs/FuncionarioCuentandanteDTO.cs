namespace Almacen.DTOs;

public class FuncionarioCuentandanteDTO
{
    public int Id { get; set; }
    public string Cedula { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string? Cargo { get; set; }
    public string? TipoVinculacion { get; set; }
    public string? SedeNombre { get; set; }

    public int BienesAsignados { get; set; }
    public decimal ValorTotalBienes { get; set; }
}