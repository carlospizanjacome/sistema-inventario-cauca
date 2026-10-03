namespace Almacen.DTOs;

// ═══════════════════════════════════════════════════════════
// KPIs PRINCIPALES
// ═══════════════════════════════════════════════════════════
public class DashboardKpisDto
{
    // Inventario
    public int TotalBienes { get; set; }
    public int TotalDevolutivos { get; set; }
    public int TotalConsumibles { get; set; }

    // Valoración
    public decimal ValorInventario { get; set; }
    public decimal ValorPPE { get; set; }
    public decimal DepreciacionAcumulada { get; set; }
    public decimal ValorNeto { get; set; }
    public decimal ValorStockConsumo { get; set; }

    // Alertas (contadores)
    public int BienesSinResponsable { get; set; }
    public int BienesSinUbicacion { get; set; }
    public int BienesBajoStock { get; set; }
    public int PrestamosVencidos { get; set; }
    public int GarantiasPorVencer { get; set; }

    // Usuarios
    public int UsuariosActivos { get; set; }
}

// ═══════════════════════════════════════════════════════════
// GRÁFICO 1: Bienes por categoría (bar chart)
// ═══════════════════════════════════════════════════════════
public class BienesPorCategoriaDto
{
    public int CategoriaId { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal ValorTotal { get; set; }
}

// ═══════════════════════════════════════════════════════════
// TABLA: Últimos bienes registrados
// ═══════════════════════════════════════════════════════════
public class UltimoBienDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string CategoriaNombre { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public DateTime? FechaRegistro { get; set; }
}

// ═══════════════════════════════════════════════════════════
// GRÁFICO 2: Depreciación mensual (line chart)
// ═══════════════════════════════════════════════════════════
public class DepreciacionMensualDto
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    public string Etiqueta { get; set; } = string.Empty;
    public decimal DepreciacionMes { get; set; }
    public decimal DepreciacionAcumulada { get; set; }
}

// ═══════════════════════════════════════════════════════════
// TABLA: Top 5 bienes por valor
// ═══════════════════════════════════════════════════════════
public class TopBienDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string CategoriaNombre { get; set; } = string.Empty;
    public decimal ValorAdquisicion { get; set; }
    public decimal ValorNeto { get; set; }
    public decimal DepreciacionAcumulada { get; set; }
}