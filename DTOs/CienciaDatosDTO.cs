namespace Almacen.DTOs;

// ═══════════════════════════════════════════════════════════════
// DTOs de Ciencia de Datos — Fase 1 (100% C#)
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Estadísticas generales del inventario (vista v_stats_inventario)
/// </summary>
public class StatsInventarioDTO
{
    public int InstitucionId { get; set; }
    public string TipoBien { get; set; } = "";
    public int TotalBienes { get; set; }
    public int TotalBajas { get; set; }
    public decimal ValorTotal { get; set; }
    public decimal ValorPromedio { get; set; }
    public decimal ValorMinimo { get; set; }
    public decimal ValorMaximo { get; set; }
    public decimal DesviacionEstandar { get; set; }
    public decimal DepreciacionTotal { get; set; }
    public decimal ValorNetoTotal { get; set; }

    /// <summary>
    /// Coeficiente de Variación: mide la dispersión relativa de los valores.
    /// < 25% = baja dispersión (homogéneo)
    /// 25-50% = dispersión moderada
    /// > 50% = alta dispersión (heterogéneo)
    /// </summary>
    public decimal CoeficienteVariacion => ValorPromedio == 0
        ? 0
        : Math.Round((DesviacionEstandar / ValorPromedio) * 100, 2);
}

/// <summary>
/// Distribución por categoría CGN (vista v_distribucion_categoria)
/// </summary>
public class DistribucionCategoriaDTO
{
    public int CategoriaId { get; set; }
    public string CategoriaNombre { get; set; } = "";
    public string CodigoCgn { get; set; } = "";
    public string TipoBien { get; set; } = "";
    public int Cantidad { get; set; }
    public decimal ValorTotal { get; set; }
    public decimal ValorPromedio { get; set; }
    public decimal DepreciacionTotal { get; set; }
}

/// <summary>
/// Detección de anomalías por Z-score (vista v_anomalias_valor)
/// </summary>
public class AnomaliaValorDTO
{
    public int BienId { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string TipoBien { get; set; } = "";
    public decimal ValorAdquisicion { get; set; }
    public decimal Media { get; set; }
    public decimal Desv { get; set; }
    public decimal ZScore { get; set; }
    public string Clasificacion { get; set; } = "";
}

/// <summary>
/// Tendencia mensual de entradas (vista v_tendencia_mensual)
/// </summary>
public class TendenciaMensualDTO
{
    public DateTime Mes { get; set; }
    public int TotalEntradas { get; set; }
    public decimal ValorEntradas { get; set; }
    public int BienesDistintos { get; set; }
}

/// <summary>
/// Ranking de responsables (vista v_ranking_responsables)
/// </summary>
public class RankingResponsableDTO
{
    public int FuncionarioId { get; set; }
    public string Funcionario { get; set; } = "";
    public string Cargo { get; set; } = "";
    public int TotalBienes { get; set; }
    public decimal ValorTotal { get; set; }
    public decimal ValorPromedio { get; set; }
}

/// <summary>
/// Proyección de stock de consumibles (vista v_proyeccion_stock)
/// </summary>
public class ProyeccionStockDTO
{
    public int BienId { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public decimal StockActual { get; set; }
    public decimal StockMinimo { get; set; }
    public decimal Cpp { get; set; }
    public decimal ValorStock { get; set; }
    public decimal ConsumoPromedio { get; set; }
    public decimal? MesesRestantes { get; set; }
    public string EstadoProyeccion { get; set; } = "";
}