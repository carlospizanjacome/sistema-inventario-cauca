namespace Almacen.DTOs;

public class ConsolidadoGlobalDTO
{
    public int TotalInstituciones { get; set; }
    public int TotalBienes { get; set; }
    public int TotalDevolutivos { get; set; }
    public int TotalConsumo { get; set; }
    public int TotalFuncionarios { get; set; }
    public int TotalUsuarios { get; set; }
    public int BienesBajoStock { get; set; }

    public decimal ValorAdquisicion { get; set; }
    public decimal DepreciacionAcumulada { get; set; }
    public decimal ValorNeto { get; set; }

    public decimal ValorStockConsumo { get; set; }

    public decimal TotalActivoLibros => ValorNeto + ValorStockConsumo;
}