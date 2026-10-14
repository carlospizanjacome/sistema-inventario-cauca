namespace Almacen.DTOs;

// ═══════════════════════════════════════════════════════════════
// DTOs de Alertas IA
// ═══════════════════════════════════════════════════════════════

public class AlertaStockDTO
{
    public int BienId { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public decimal StockActual { get; set; }
    public decimal StockMinimo { get; set; }
    public decimal Faltante { get; set; }
    public decimal Cpp { get; set; }
    public string Severidad { get; set; } = "";
}

public class AlertaPrestamoDTO
{
    public int PrestamoId { get; set; }
    public int BienId { get; set; }
    public string BienCodigo { get; set; } = "";
    public string BienNombre { get; set; } = "";
    public string Funcionario { get; set; } = "";
    public DateTime FechaPrestamo { get; set; }
    public DateTime FechaDevolucionPrevista { get; set; }
    public int DiasRetraso { get; set; }
    public string Severidad { get; set; } = "";
}

public class AlertaSinResponsableDTO
{
    public int BienId { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public decimal ValorAdquisicion { get; set; }
    public string? Categoria { get; set; }
    public string? Aula { get; set; }
}

public class AlertaSinUbicacionDTO
{
    public int BienId { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public decimal ValorAdquisicion { get; set; }
    public string? Categoria { get; set; }
    public string? Responsable { get; set; }
}

public class AlertasResumenDTO
{
    public int TotalAlertas { get; set; }
    public int StockCritico { get; set; }
    public int PrestamosVencidos { get; set; }
    public int SinResponsable { get; set; }
    public int SinUbicacion { get; set; }
    public int Criticas { get; set; }
    public int Altas { get; set; }
    public int Medias { get; set; }
}