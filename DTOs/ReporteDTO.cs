namespace Almacen.DTOs;

public class ConsolidadoInstitucionDTO
{
    public int InstitucionId { get; set; }
    public string InstitucionNombre { get; set; } = string.Empty;
    public string? InstitucionNit { get; set; }
    public string? CodigoDane { get; set; }

    public int TotalBienes { get; set; }
    public int TotalDevolutivos { get; set; }
    public int TotalConsumo { get; set; }
    public int TotalFuncionarios { get; set; }
    public int TotalUsuarios { get; set; }
    public int TotalSedes { get; set; }
    public int BienesBajoStock { get; set; }

    public decimal ValorAdquisicion { get; set; }
    public decimal DepreciacionAcumulada { get; set; }
    public decimal ValorNeto { get; set; }
}

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
}

public class FiltroReporteDTO
{
    public int? InstitucionId { get; set; }
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
}