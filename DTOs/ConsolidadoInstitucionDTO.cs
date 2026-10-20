public class ConsolidadoInstitucionDTO
{
    public int InstitucionId { get; set; }
    public string InstitucionNombre { get; set; } = "";
    public string? InstitucionNit { get; set; }
    public string? CodigoDane { get; set; }
    public int SedeId { get; set; }
    public string SedeNombre { get; set; } = "";

    public int TotalBienes { get; set; }
    public int TotalDevolutivos { get; set; }
    public int TotalConsumo { get; set; }
    public int TotalFuncionarios { get; set; }
    public int TotalUsuarios { get; set; }
    public int TotalSedes { get; set; }

    public decimal ValorAdquisicion { get; set; }
    public decimal DepreciacionAcumulada { get; set; }
    public decimal ValorNeto { get; set; }
    public decimal ValorStockConsumo { get; set; }

    // 👇 ESTA ES LA LÍNEA MÁGICA QUE FALTA
    public decimal TotalActivoLibros => ValorNeto + ValorStockConsumo;

    public int BienesBajoStock { get; set; }
}