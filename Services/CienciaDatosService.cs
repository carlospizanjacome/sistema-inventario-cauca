using Dapper;
using Almacen.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Almacen.Services;

/// <summary>
/// Servicio de Ciencia de Datos — Fase 1 (100% C#)
/// Crea conexiones a PostgreSQL directamente desde IConfiguration.
/// Cero dependencias de factories externas.
/// </summary>
public class CienciaDatosService
{
    private readonly string _connectionString;
    private readonly ILogger<CienciaDatosService> _logger;

    public CienciaDatosService(IConfiguration configuration, ILogger<CienciaDatosService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "La cadena de conexión 'DefaultConnection' no está configurada.");
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════
    // MÉTODO PRIVADO — Crea conexión nueva por operación
    // ═══════════════════════════════════════════════════════════
    private async Task<NpgsqlConnection> CrearConexionAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    // ═══════════════════════════════════════════════════════════
    // 1. ESTADÍSTICAS GENERALES
    // ═══════════════════════════════════════════════════════════
    public async Task<List<StatsInventarioDTO>> ObtenerStatsAsync(int institucionId)
    {
        const string sql = @"
            SELECT 
                institucion_id AS InstitucionId,
                tipo_bien AS TipoBien,
                total_bienes AS TotalBienes,
                total_bajas AS TotalBajas,
                valor_total AS ValorTotal,
                valor_promedio AS ValorPromedio,
                valor_minimo AS ValorMinimo,
                valor_maximo AS ValorMaximo,
                desviacion_estandar AS DesviacionEstandar,
                depreciacion_total AS DepreciacionTotal,
                valor_neto_total AS ValorNetoTotal
            FROM v_stats_inventario
            WHERE institucion_id = @InstitucionId";

        await using var db = await CrearConexionAsync();
        var resultado = await db.QueryAsync<StatsInventarioDTO>(sql, new { InstitucionId = institucionId });
        return resultado.ToList();
    }

    // ═══════════════════════════════════════════════════════════
    // 2. DISTRIBUCIÓN POR CATEGORÍA
    // ═══════════════════════════════════════════════════════════
    public async Task<List<DistribucionCategoriaDTO>> ObtenerDistribucionAsync(int institucionId)
    {
        const string sql = @"
            SELECT 
                categoria_id AS CategoriaId,
                categoria_nombre AS CategoriaNombre,
                codigo_cgn AS CodigoCgn,
                tipo_bien AS TipoBien,
                cantidad AS Cantidad,
                valor_total AS ValorTotal,
                valor_promedio AS ValorPromedio,
                depreciacion_total AS DepreciacionTotal
            FROM v_distribucion_categoria
            WHERE institucion_id = @InstitucionId
            ORDER BY valor_total DESC";

        await using var db = await CrearConexionAsync();
        var resultado = await db.QueryAsync<DistribucionCategoriaDTO>(sql, new { InstitucionId = institucionId });
        return resultado.ToList();
    }

    // ═══════════════════════════════════════════════════════════
    // 3. DETECCIÓN DE ANOMALÍAS (Z-score)
    // ═══════════════════════════════════════════════════════════
    public async Task<List<AnomaliaValorDTO>> ObtenerAnomaliasAsync(int institucionId, string? filtro = null)
    {
        var sql = @"
            SELECT 
                bien_id AS BienId,
                codigo AS Codigo,
                nombre AS Nombre,
                tipo_bien AS TipoBien,
                valor_adquisicion AS ValorAdquisicion,
                media AS Media,
                desv AS Desv,
                z_score AS ZScore,
                clasificacion AS Clasificacion
            FROM v_anomalias_valor
            WHERE institucion_id = @InstitucionId";

        if (!string.IsNullOrEmpty(filtro))
            sql += " AND clasificacion = @Filtro";

        sql += " ORDER BY ABS(z_score) DESC";

        await using var db = await CrearConexionAsync();
        var resultado = await db.QueryAsync<AnomaliaValorDTO>(sql,
            new { InstitucionId = institucionId, Filtro = filtro });
        return resultado.ToList();
    }

    // ═══════════════════════════════════════════════════════════
    // 4. TENDENCIAS MENSUALES
    // ═══════════════════════════════════════════════════════════
    public async Task<List<TendenciaMensualDTO>> ObtenerTendenciasAsync(int institucionId, int meses = 12)
    {
        const string sql = @"
            SELECT 
                mes AS Mes,
                total_entradas AS TotalEntradas,
                valor_entradas AS ValorEntradas,
                bienes_distintos AS BienesDistintos
            FROM v_tendencia_mensual
            WHERE institucion_id = @InstitucionId
            ORDER BY mes ASC
            LIMIT @Meses";

        await using var db = await CrearConexionAsync();
        var resultado = await db.QueryAsync<TendenciaMensualDTO>(sql,
            new { InstitucionId = institucionId, Meses = meses });
        return resultado.ToList();
    }

    // ═══════════════════════════════════════════════════════════
    // 5. RANKING DE RESPONSABLES
    // ═══════════════════════════════════════════════════════════
    public async Task<List<RankingResponsableDTO>> ObtenerRankingResponsablesAsync(int institucionId, int top = 10)
    {
        const string sql = @"
            SELECT 
                funcionario_id AS FuncionarioId,
                funcionario AS Funcionario,
                cargo AS Cargo,
                total_bienes AS TotalBienes,
                valor_total AS ValorTotal,
                valor_promedio AS ValorPromedio
            FROM v_ranking_responsables
            WHERE institucion_id = @InstitucionId
            ORDER BY total_bienes DESC
            LIMIT @Top";

        await using var db = await CrearConexionAsync();
        var resultado = await db.QueryAsync<RankingResponsableDTO>(sql,
            new { InstitucionId = institucionId, Top = top });
        return resultado.ToList();
    }

    // ═══════════════════════════════════════════════════════════
    // 6. PROYECCIÓN DE STOCK
    // ═══════════════════════════════════════════════════════════
    public async Task<List<ProyeccionStockDTO>> ObtenerProyeccionesAsync(int institucionId)
    {
        const string sql = @"
            SELECT 
                bien_id AS BienId,
                codigo AS Codigo,
                nombre AS Nombre,
                stock_actual AS StockActual,
                stock_minimo AS StockMinimo,
                cpp AS Cpp,
                valor_stock AS ValorStock,
                consumo_promedio AS ConsumoPromedio,
                meses_restantes AS MesesRestantes,
                estado_proyeccion AS EstadoProyeccion
            FROM v_proyeccion_stock
            WHERE institucion_id = @InstitucionId
            ORDER BY 
                CASE estado_proyeccion 
                    WHEN 'CRITICO' THEN 1 
                    WHEN 'ALERTA' THEN 2 
                    ELSE 3 
                END,
                meses_restantes ASC NULLS LAST";

        await using var db = await CrearConexionAsync();
        var resultado = await db.QueryAsync<ProyeccionStockDTO>(sql, new { InstitucionId = institucionId });
        return resultado.ToList();
    }

    // ═══════════════════════════════════════════════════════════
    // 7. REFRESH de vistas materializadas
    // ═══════════════════════════════════════════════════════════
    public async Task RefrescarVistasAsync()
    {
        const string sql = @"
            REFRESH MATERIALIZED VIEW v_stats_inventario;
            REFRESH MATERIALIZED VIEW v_distribucion_categoria;
            REFRESH MATERIALIZED VIEW v_anomalias_valor;
            REFRESH MATERIALIZED VIEW v_tendencia_mensual;
            REFRESH MATERIALIZED VIEW v_ranking_responsables;
            REFRESH MATERIALIZED VIEW v_proyeccion_stock;";

        await using var db = await CrearConexionAsync();
        await db.ExecuteAsync(sql);
        _logger.LogInformation("Vistas materializadas refrescadas correctamente");
    }
}