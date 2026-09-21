namespace Almacen.DTOs;

public class DepreciacionDTO
{
    public int Id { get; set; }
    public int BienId { get; set; }
    public int PeriodoAnio { get; set; }
    public int PeriodoMes { get; set; }
    public decimal ValorInicial { get; set; }
    public decimal ValorResidual { get; set; }
    public decimal DepreciacionMes { get; set; }
    public decimal DepreciacionAcumulada { get; set; }
    public decimal ValorNeto { get; set; }

    // Navegación
    public string? BienCodigo { get; set; }
    public string? BienNombre { get; set; }
    public string? CategoriaNombre { get; set; }
    public string? VidaUtilDescripcion { get; set; }
    public int? VidaUtilMeses { get; set; }
    public DateTime? FechaAdquisicion { get; set; }
}

// DTO de resumen por bien (vista principal)
public class BienDepreciacionDTO
{
    public int BienId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? CategoriaNombre { get; set; }
    public DateTime? FechaAdquisicion { get; set; }
    public int? VidaUtilMeses { get; set; }
    public decimal ValorInicial { get; set; }
    public decimal ValorResidual { get; set; }
    public decimal DepreciacionMensual { get; set; }
    public decimal DepreciacionAcumulada { get; set; }
    public decimal ValorNeto { get; set; }
    public int MesesTranscurridos { get; set; }
    public decimal PorcentajeDepreciado { get; set; }
    public bool CompletamenteDepreciado { get; set; }
}

// DTO de resultado del cálculo
public class ResultadoDepreciacionDTO
{
    public int TotalBienes { get; set; }
    public int BienesCalculados { get; set; }
    public int BienesOmitidos { get; set; }
    public decimal DepreciacionTotalMes { get; set; }
    public DateTime FechaEjecucion { get; set; }
    public List<string> Advertencias { get; set; } = new();
}