namespace Almacen.DTOs;

public class DashboardKpisDto
{
    public int TotalBienes { get; set; }
    public int TotalCategorias { get; set; }
    public int UsuariosActivos { get; set; }
    public int TotalRoles { get; set; }
    public decimal ValorInventario { get; set; }
}

public class BienesPorCategoriaDto
{
    public int CategoriaId { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal ValorTotal { get; set; }
}

public class UltimoBienDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string CategoriaNombre { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public DateTime? FechaRegistro { get; set; }
}