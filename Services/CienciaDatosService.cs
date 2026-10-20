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

    // Helper centralizado para generar el filtro de sede y sus parámetros
    private (string Sql, DynamicParameters Parametros) GenerarFiltro(int? sedeIdFiltro)
    {
        var sql = "";
        var p = new DynamicParameters();

        if (sedeIdFiltro.HasValue && sedeIdFiltro > 0)
        {
            // Si el usuario (Rector) seleccionó una sede específica en el combo
            sql = " AND sede_id = @SedeFiltro";
            p.Add("SedeFiltro", sedeIdFiltro.Value);
        }
        else if (!_sesion.VeTodasLasSedes && _sesion.SedeId.HasValue)
        {
            // Si es Almacenista (no ve todas las sedes)
            sql = " AND sede_id = @SedeFiltro";
            p.Add("SedeFiltro", _sesion.SedeId.Value);
        }

        return (sql, p);
    }

    public async Task<List<StatsInventarioDTO>> ObtenerStatsAsync(int? sedeIdFiltro = null)
    {
        var (filtro, paramSede) = GenerarFiltro(sedeIdFiltro);
        var parametros = new DynamicParameters(paramSede);
        parametros.Add("InstitucionId", _sesion.InstitucionId);

        var sql = $@"
            SELECT 
                @InstitucionId AS InstitucionId, 
                tipo_bien AS TipoBien,
                SUM(total_bienes)::int AS TotalBienes, 
                SUM(total_bajas)::int AS TotalBajas,
                SUM(valor_total) AS ValorTotal, 
                CASE WHEN SUM(total_bienes) > 0 THEN SUM(valor_total) / SUM(total_bienes) ELSE 0 END AS ValorPromedio,
                MIN(valor_minimo) AS ValorMinimo, 
                MAX(valor_maximo) AS ValorMaximo,
                AVG(desviacion_estandar) AS DesviacionEstandar, 
                SUM(depreciacion_total) AS DepreciacionTotal,
                SUM(valor_neto_total) AS ValorNetoTotal
            FROM v_stats_inventario
            WHERE institucion_id = @InstitucionId {filtro}
            GROUP BY tipo_bien";

        await using var db = await CrearConexionAsync();
        var resultado = await db.QueryAsync<StatsInventarioDTO>(sql, parametros);
        return resultado.ToList();
    }

    public async Task<List<DistribucionCategoriaDTO>> ObtenerDistribucionAsync(int? sedeIdFiltro = null)
    {
        var (filtro, paramSede) = GenerarFiltro(sedeIdFiltro);
        var parametros = new DynamicParameters(paramSede);
        parametros.Add("InstitucionId", _sesion.InstitucionId);

        var sql = $@"
            SELECT 
                categoria_id AS CategoriaId, 
                categoria_nombre AS CategoriaNombre,
                codigo_cgn AS CodigoCgn, 
                tipo_bien AS TipoBien,
                SUM(cantidad)::int AS Cantidad, 
                SUM(valor_total) AS ValorTotal,
                CASE WHEN SUM(cantidad) > 0 THEN SUM(valor_total) / SUM(cantidad) ELSE 0 END AS ValorPromedio, 
                SUM(depreciacion_total) AS DepreciacionTotal
            FROM v_distribucion_categoria
            WHERE institucion_id = @InstitucionId {filtro}
            GROUP BY categoria_id, categoria_nombre, codigo_cgn, tipo_bien
            ORDER BY SUM(valor_total) DESC";

        await using var db = await CrearConexionAsync();
        var resultado = await db.QueryAsync<DistribucionCategoriaDTO>(sql, parametros);
        return resultado.ToList();
    }

    public async Task<List<AnomaliaValorDTO>> ObtenerAnomaliasAsync(int? sedeIdFiltro = null, string? filtroClasificacion = null)
    {
        var (filtro, paramSede) = GenerarFiltro(sedeIdFiltro);
        var parametros = new DynamicParameters(paramSede);
        parametros.Add("InstitucionId", _sesion.InstitucionId);

        var sql = $@"
            SELECT 
                bien_id AS BienId, codigo AS Codigo, nombre AS Nombre,
                tipo_bien AS TipoBien, valor_adquisicion AS ValorAdquisicion,
                media AS Media, desv AS Desv, z_score AS ZScore, clasificacion AS Clasificacion
            FROM v_anomalias_valor
            WHERE institucion_id = @InstitucionId {filtro}";

        if (!string.IsNullOrEmpty(filtroClasificacion))
        {
            sql += " AND clasificacion = @FiltroClasificacion";
            parametros.Add("FiltroClasificacion", filtroClasificacion);
        }

        sql += " ORDER BY ABS(z_score) DESC";

        await using var db = await CrearConexionAsync();
        var resultado = await db.QueryAsync<AnomaliaValorDTO>(sql, parametros);
        return resultado.ToList();
    }

    public async Task<List<TendenciaMensualDTO>> ObtenerTendenciasAsync(int? sedeIdFiltro = null, int meses = 12)
    {
        var (filtro, paramSede) = GenerarFiltro(sedeIdFiltro);
        var parametros = new DynamicParameters(paramSede);
        parametros.Add("InstitucionId", _sesion.InstitucionId);
        parametros.Add("Meses", meses);

        var sql = $@"
            SELECT 
                mes AS Mes, 
                SUM(total_entradas)::int AS TotalEntradas,
                SUM(valor_entradas) AS ValorEntradas, 
                SUM(bienes_distintos)::int AS BienesDistintos
            FROM v_tendencia_mensual
            WHERE institucion_id = @InstitucionId {filtro}
            GROUP BY mes
            ORDER BY mes ASC
            LIMIT @Meses";

        await using var db = await CrearConexionAsync();
        var resultado = await db.QueryAsync<TendenciaMensualDTO>(sql, parametros);
        return resultado.ToList();
    }

    public async Task<List<RankingResponsableDTO>> ObtenerRankingResponsablesAsync(int? sedeIdFiltro = null, int top = 10)
    {
        var (filtro, paramSede) = GenerarFiltro(sedeIdFiltro);
        var parametros = new DynamicParameters(paramSede);
        parametros.Add("InstitucionId", _sesion.InstitucionId);
        parametros.Add("Top", top);

        var sql = $@"
            SELECT 
                funcionario_id AS FuncionarioId, funcionario AS Funcionario,
                cargo AS Cargo, 
                SUM(total_bienes)::int AS TotalBienes,
                SUM(valor_total) AS ValorTotal, 
                CASE WHEN SUM(total_bienes) > 0 THEN SUM(valor_total) / SUM(total_bienes) ELSE 0 END AS ValorPromedio
            FROM v_ranking_responsables
            WHERE institucion_id = @InstitucionId {filtro}
            GROUP BY funcionario_id, funcionario, cargo
            ORDER BY SUM(total_bienes) DESC
            LIMIT @Top";

        await using var db = await CrearConexionAsync();
        var resultado = await db.QueryAsync<RankingResponsableDTO>(sql, parametros);
        return resultado.ToList();
    }

    public async Task<List<ProyeccionStockDTO>> ObtenerProyeccionesAsync(int? sedeIdFiltro = null)
    {
        var (filtro, paramSede) = GenerarFiltro(sedeIdFiltro);
        var parametros = new DynamicParameters(paramSede);
        parametros.Add("InstitucionId", _sesion.InstitucionId);

        var sql = $@"
            SELECT 
                bien_id AS BienId, codigo AS Codigo, nombre AS Nombre,
                stock_actual AS StockActual, stock_minimo AS StockMinimo,
                cpp AS Cpp, valor_stock AS ValorStock,
                consumo_promedio AS ConsumoPromedio, meses_restantes AS MesesRestantes,
                estado_proyeccion AS EstadoProyeccion
            FROM v_proyeccion_stock
            WHERE institucion_id = @InstitucionId {filtro}
            ORDER BY 
                CASE estado_proyeccion WHEN 'CRITICO' THEN 1 WHEN 'ALERTA' THEN 2 ELSE 3 END,
                meses_restantes ASC NULLS LAST";

        await using var db = await CrearConexionAsync();
        var resultado = await db.QueryAsync<ProyeccionStockDTO>(sql, parametros);
        return resultado.ToList();
    }

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