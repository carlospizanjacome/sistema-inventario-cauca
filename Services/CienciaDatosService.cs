using Dapper;
using Almacen.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Almacen.Services;

public class CienciaDatosService
{
    private readonly string _connectionString;
    private readonly ILogger<CienciaDatosService> _logger;
    private readonly UsuarioSesionService _sesion;

    public CienciaDatosService(
        IConfiguration configuration,
        ILogger<CienciaDatosService> logger,
        UsuarioSesionService sesion)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
        _logger = logger;
        _sesion = sesion;
    }

    private async Task<NpgsqlConnection> CrearConexionAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    // Helper centralizado para generar el filtro de sede
    private (string Sql, DynamicParameters Parametros) GenerarFiltroSede(int? sedeIdFiltro, string alias = "")
    {
        var sql = "";
        var p = new DynamicParameters();

        if (sedeIdFiltro.HasValue && sedeIdFiltro > 0)
        {
            sql = $" AND {alias}sede_id = @SedeFiltro";
            p.Add("SedeFiltro", sedeIdFiltro.Value);
        }
        else if (!_sesion.VeTodasLasSedes && _sesion.SedeId.HasValue)
        {
            sql = $" AND {alias}sede_id = @SedeFiltro";
            p.Add("SedeFiltro", _sesion.SedeId.Value);
        }

        return (sql, p);
    }

    // ═════════════════════════════════════════════════════════════════
    // 1. ESTADÍSTICAS DE INVENTARIO (Devolutivos + Consumibles)
    // ═════════════════════════════════════════════════════════════════
    public async Task<List<StatsInventarioDTO>> ObtenerStatsAsync(int? sedeIdFiltro = null)
    {
        var (filtroSede, parametros) = GenerarFiltroSede(sedeIdFiltro, "b.");
        parametros.Add("InstitucionId", _sesion.InstitucionId);

        var sql = $@"
            -- Devolutivos (PPE)
            SELECT 
                @InstitucionId AS InstitucionId, 
                'devolutivo'::text AS TipoBien,
                COUNT(b.id)::int AS TotalBienes, 
                COUNT(b.id) FILTER (WHERE b.activo = FALSE)::int AS TotalBajas,
                COALESCE(SUM(b.valor_adquisicion), 0)::numeric AS ValorTotal,
                COALESCE(AVG(b.valor_adquisicion), 0)::numeric AS ValorPromedio,
                COALESCE(MIN(b.valor_adquisicion), 0)::numeric AS ValorMinimo,
                COALESCE(MAX(b.valor_adquisicion), 0)::numeric AS ValorMaximo,
                COALESCE(STDDEV(b.valor_adquisicion), 0)::numeric AS DesviacionEstandar,
                COALESCE(SUM(b.depreciacion_acumulada), 0)::numeric AS DepreciacionTotal,
                COALESCE(SUM(b.valor_neto), 0)::numeric AS ValorNetoTotal
            FROM bienes b
            WHERE b.institucion_id = @InstitucionId 
              AND b.tipo_bien = 'devolutivo'
              AND b.activo = TRUE -- ✅ Solo activos
              {filtroSede}

            UNION ALL

            -- Consumibles (Stock * CPP)
            SELECT 
                @InstitucionId AS InstitucionId, 
                'consumo'::text AS TipoBien,
                COUNT(b.id)::int AS TotalBienes, 
                COUNT(b.id) FILTER (WHERE b.activo = FALSE)::int AS TotalBajas,
                COALESCE(SUM(b.stock_actual * b.cpp_actual), 0)::numeric AS ValorTotal,
                COALESCE(AVG(b.stock_actual * b.cpp_actual), 0)::numeric AS ValorPromedio,
                COALESCE(MIN(b.stock_actual * b.cpp_actual), 0)::numeric AS ValorMinimo,
                COALESCE(MAX(b.stock_actual * b.cpp_actual), 0)::numeric AS ValorMaximo,
                COALESCE(STDDEV(b.stock_actual * b.cpp_actual), 0)::numeric AS DesviacionEstandar,
                0::numeric AS DepreciacionTotal,
                COALESCE(SUM(b.stock_actual * b.cpp_actual), 0)::numeric AS ValorNetoTotal
            FROM bienes b
            WHERE b.institucion_id = @InstitucionId 
              AND b.tipo_bien = 'consumo'
              AND b.activo = TRUE -- ✅ Solo activos
              {filtroSede}";

        await using var db = await CrearConexionAsync();
        var resultado = await db.QueryAsync<StatsInventarioDTO>(sql, parametros);
        return resultado.ToList();
    }

    // ═════════════════════════════════════════════════════════════════
    // 2. DISTRIBUCIÓN POR CATEGORÍA CGN
    // ═════════════════════════════════════════════════════════════════
    public async Task<List<DistribucionCategoriaDTO>> ObtenerDistribucionAsync(int? sedeIdFiltro = null)
    {
        var (filtroSede, parametros) = GenerarFiltroSede(sedeIdFiltro, "b.");
        parametros.Add("InstitucionId", _sesion.InstitucionId);

        var sql = $@"
            SELECT 
                COALESCE(c.id, 0) AS CategoriaId, 
                COALESCE(c.nombre, 'Sin categoría') AS CategoriaNombre,
                COALESCE(c.codigo_cgn, '—') AS CodigoCgn, 
                b.tipo_bien AS TipoBien,
                COUNT(b.id)::int AS Cantidad, 
                COALESCE(SUM(
                    CASE 
                        WHEN b.tipo_bien = 'devolutivo' THEN b.valor_adquisicion
                        ELSE (b.stock_actual * b.cpp_actual)
                    END
                ), 0)::numeric AS ValorTotal,
                COALESCE(AVG(
                    CASE 
                        WHEN b.tipo_bien = 'devolutivo' THEN b.valor_adquisicion
                        ELSE (b.stock_actual * b.cpp_actual)
                    END
                ), 0)::numeric AS ValorPromedio, 
                COALESCE(SUM(
                    CASE 
                        WHEN b.tipo_bien = 'devolutivo' THEN b.depreciacion_acumulada
                        ELSE 0
                    END
                ), 0)::numeric AS DepreciacionTotal
            FROM bienes b
            LEFT JOIN categorias c ON c.id = b.categoria_id
            WHERE b.institucion_id = @InstitucionId 
              AND b.activo = TRUE
              {filtroSede}
            GROUP BY c.id, c.nombre, c.codigo_cgn, b.tipo_bien
            ORDER BY SUM(
                CASE 
                    WHEN b.tipo_bien = 'devolutivo' THEN b.valor_adquisicion
                    ELSE (b.stock_actual * b.cpp_actual)
                END
            ) DESC";

        await using var db = await CrearConexionAsync();
        var resultado = await db.QueryAsync<DistribucionCategoriaDTO>(sql, parametros);
        return resultado.ToList();
    }

    // ═════════════════════════════════════════════════════════════════
    // 3. ANOMALÍAS (Solo Devolutivos con Z-Score)
    // ═════════════════════════════════════════════════════════════════
    public async Task<List<AnomaliaValorDTO>> ObtenerAnomaliasAsync(int? sedeIdFiltro = null, string? filtroClasificacion = null)
    {
        var (filtroSede, parametros) = GenerarFiltroSede(sedeIdFiltro, "b.");
        parametros.Add("InstitucionId", _sesion.InstitucionId);

        // Solo bienes devolutivos activos con valor > 0
        var sql = $@"
            WITH stats AS (
                SELECT 
                    COALESCE(AVG(valor_adquisicion), 0) AS media,
                    COALESCE(STDDEV(valor_adquisicion), 0) AS desv
                FROM bienes
                WHERE institucion_id = @InstitucionId 
                  AND activo = TRUE 
                  AND tipo_bien = 'devolutivo'
                  AND valor_adquisicion > 0
                  {filtroSede}
            )
            SELECT 
                b.id AS BienId, 
                b.codigo AS Codigo, 
                b.nombre AS Nombre,
                b.tipo_bien AS TipoBien, 
                b.valor_adquisicion AS ValorAdquisicion,
                s.media AS Media, 
                s.desv AS Desv,
                CASE 
                    WHEN s.desv > 0 THEN (b.valor_adquisicion - s.media) / s.desv 
                    ELSE 0 
                END AS ZScore,
                CASE 
                    WHEN s.desv = 0 THEN 'NORMAL'
                    WHEN ABS((b.valor_adquisicion - s.media) / s.desv) > 2 THEN 'ANOMALO'
                    WHEN ABS((b.valor_adquisicion - s.media) / s.desv) > 1.5 THEN 'REVISAR'
                    ELSE 'NORMAL'
                END AS Clasificacion
            FROM bienes b, stats s
            WHERE b.institucion_id = @InstitucionId 
              AND b.activo = TRUE 
              AND b.tipo_bien = 'devolutivo'
              AND b.valor_adquisicion > 0
              {filtroSede}";

        if (!string.IsNullOrEmpty(filtroClasificacion))
        {
            sql += " AND (CASE WHEN s.desv = 0 THEN 'NORMAL' WHEN ABS((b.valor_adquisicion - s.media) / s.desv) > 2 THEN 'ANOMALO' WHEN ABS((b.valor_adquisicion - s.media) / s.desv) > 1.5 THEN 'REVISAR' ELSE 'NORMAL' END) = @FiltroClasificacion";
            parametros.Add("FiltroClasificacion", filtroClasificacion);
        }

        sql += " ORDER BY ABS(CASE WHEN s.desv > 0 THEN (b.valor_adquisicion - s.media) / s.desv ELSE 0 END) DESC";

        await using var db = await CrearConexionAsync();
        var resultado = await db.QueryAsync<AnomaliaValorDTO>(sql, parametros);
        return resultado.ToList();
    }

    // ═════════════════════════════════════════════════════════════════
    // 4. TENDENCIAS MENSUALES
    // ═════════════════════════════════════════════════════════════════
    public async Task<List<TendenciaMensualDTO>> ObtenerTendenciasAsync(int? sedeIdFiltro = null, int meses = 12)
    {
        var (filtroSede, parametros) = GenerarFiltroSede(sedeIdFiltro, "e.");
        parametros.Add("InstitucionId", _sesion.InstitucionId);
        parametros.Add("Meses", meses);

        var sql = $@"
            SELECT 
                DATE_TRUNC('month', e.fecha_entrada)::timestamp AS Mes,
                COUNT(*)::int AS TotalEntradas,
                COALESCE(SUM(e.valor), 0)::numeric AS ValorEntradas,
                COUNT(DISTINCT e.bien_id)::int AS BienesDistintos
            FROM entradas e
            WHERE e.institucion_id = @InstitucionId 
              AND e.anulada = FALSE
              {filtroSede}
            GROUP BY DATE_TRUNC('month', e.fecha_entrada)
            ORDER BY Mes DESC
            LIMIT @Meses";

        await using var db = await CrearConexionAsync();
        var resultado = await db.QueryAsync<TendenciaMensualDTO>(sql, parametros);
        // Ordenamos de más antiguo a más reciente para el gráfico
        return resultado.OrderBy(t => t.Mes).ToList();
    }

    // ═════════════════════════════════════════════════════════════════
    // 5. RANKING DE RESPONSABLES
    // ═════════════════════════════════════════════════════════════════
    public async Task<List<RankingResponsableDTO>> ObtenerRankingResponsablesAsync(int? sedeIdFiltro = null, int top = 10)
    {
        var (filtroSede, parametros) = GenerarFiltroSede(sedeIdFiltro, "b.");
        parametros.Add("InstitucionId", _sesion.InstitucionId);
        parametros.Add("Top", top);

        var sql = $@"
            SELECT 
                f.id AS FuncionarioId, 
                f.nombre_completo AS Funcionario,
                COALESCE(f.cargo, '—') AS Cargo, 
                COUNT(b.id)::int AS TotalBienes,
                COALESCE(SUM(b.valor_adquisicion), 0)::numeric AS ValorTotal, 
                COALESCE(AVG(b.valor_adquisicion), 0)::numeric AS ValorPromedio
            FROM bienes b
            INNER JOIN funcionarios f ON f.id = b.funcionario_id
            WHERE b.institucion_id = @InstitucionId 
              AND b.activo = TRUE 
              AND b.tipo_bien = 'devolutivo'
              {filtroSede}
            GROUP BY f.id, f.nombre_completo, f.cargo
            ORDER BY COUNT(b.id) DESC
            LIMIT @Top";

        await using var db = await CrearConexionAsync();
        var resultado = await db.QueryAsync<RankingResponsableDTO>(sql, parametros);
        return resultado.ToList();
    }

    // ═════════════════════════════════════════════════════════════════
    // 6. PROYECCIONES DE STOCK
    // ═════════════════════════════════════════════════════════════════
    public async Task<List<ProyeccionStockDTO>> ObtenerProyeccionesAsync(int? sedeIdFiltro = null)
    {
        var (filtroSede, parametros) = GenerarFiltroSede(sedeIdFiltro, "b.");
        parametros.Add("InstitucionId", _sesion.InstitucionId);

        var sql = $@"
            SELECT 
                b.id AS BienId, 
                b.codigo AS Codigo, 
                b.nombre AS Nombre,
                b.stock_actual AS StockActual, 
                b.stock_minimo AS StockMinimo,
                b.cpp_actual AS Cpp, 
                (b.stock_actual * b.cpp_actual) AS ValorStock,
                COALESCE((
                    SELECT AVG(m.cantidad) 
                    FROM movimientos_consumo m
                    WHERE m.bien_id = b.id 
                      AND m.tipo_movimiento = 'SALIDA' 
                      AND m.anulada = FALSE
                      AND m.fecha_movimiento > NOW() - INTERVAL '6 months'
                ), 0)::numeric AS ConsumoPromedio,
                CASE 
                    WHEN b.stock_actual <= b.stock_minimo THEN 0
                    WHEN COALESCE((
                        SELECT AVG(m.cantidad) 
                        FROM movimientos_consumo m
                        WHERE m.bien_id = b.id AND m.tipo_movimiento = 'SALIDA' AND m.anulada = FALSE
                    ), 0) = 0 THEN NULL
                    ELSE (b.stock_actual - b.stock_minimo) / (
                        SELECT AVG(m.cantidad) 
                        FROM movimientos_consumo m
                        WHERE m.bien_id = b.id AND m.tipo_movimiento = 'SALIDA' AND m.anulada = FALSE
                    )
                END AS MesesRestantes,
                CASE 
                    WHEN b.stock_actual <= b.stock_minimo THEN 'CRITICO'
                    WHEN b.stock_actual <= b.stock_minimo * 1.5 THEN 'ALERTA'
                    ELSE 'NORMAL'
                END AS EstadoProyeccion
            FROM bienes b
            WHERE b.institucion_id = @InstitucionId 
              AND b.activo = TRUE 
              AND b.tipo_bien = 'consumo'
              {filtroSede}
            ORDER BY 
                CASE WHEN b.stock_actual <= b.stock_minimo THEN 1 WHEN b.stock_actual <= b.stock_minimo * 1.5 THEN 2 ELSE 3 END,
                b.stock_actual ASC";

        await using var db = await CrearConexionAsync();
        var resultado = await db.QueryAsync<ProyeccionStockDTO>(sql, parametros);
        return resultado.ToList();
    }

    // ═════════════════════════════════════════════════════════════════
    // REFRESCAR VISTAS (Por compatibilidad)
    // ═════════════════════════════════════════════════════════════════
    public async Task RefrescarVistasAsync()
    {
        _logger.LogInformation("Refresco de datos: consultas directas a tablas (sin vistas materializadas)");
        await Task.CompletedTask;
    }
}