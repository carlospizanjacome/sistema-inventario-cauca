using Almacen.DTOs;
using Almacen.Helpers;
using Almacen.Interfaces;
using Almacen.Services;
using Dapper;
using Npgsql;
using System.Data;

namespace Almacen.Repositories;

public class DashboardRepository : IDashboardRepository
{
    private readonly string _cs;
    private readonly UsuarioSesionService _sesion;

    public DashboardRepository(IConfiguration cfg, UsuarioSesionService sesion)
    {
        _cs = cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta DefaultConnection.");
        _sesion = sesion;
    }

    private (string Sql, DynamicParameters Parametros) ConstruirFiltro(string alias)
    {
        var sql = "";
        var p = new DynamicParameters();
        p.Add("InstitucionId", _sesion.InstitucionId);

        if (!_sesion.EsSuperAdmin)
        {
            sql += $" AND {alias}.institucion_id = @InstitucionId";
        }

        if (!_sesion.VeTodasLasSedes && _sesion.SedeId.HasValue)
        {
            sql += $" AND {alias}.sede_id = @SedeId";
            p.Add("SedeId", _sesion.SedeId.Value, DbType.Int32);
        }

        return (sql, p);
    }

    // ═══════════════════════════════════════════════════════
    // KPIs PRINCIPALES
    // ═══════════════════════════════════════════════════════
    public async Task<DashboardKpisDto> ObtenerKpisAsync()
    {
        var (filtroB, parametros) = ConstruirFiltro("b");
        var (filtroU, _) = ConstruirFiltro("u");
        var (filtroP, _) = ConstruirFiltro("p");
        var (filtroG, _) = ConstruirFiltro("g");

        var sql = $@"
            SELECT
                (SELECT COUNT(*) FROM bienes b 
                 WHERE b.activo = TRUE {filtroB}) AS TotalBienes,

                (SELECT COUNT(*) FROM bienes b 
                 WHERE b.activo = TRUE AND b.tipo_bien = 'devolutivo' {filtroB}) AS TotalDevolutivos,

                (SELECT COUNT(*) FROM bienes b 
                 WHERE b.activo = TRUE AND b.tipo_bien = 'consumo' {filtroB}) AS TotalConsumibles,

                (SELECT COALESCE(SUM(b.valor_adquisicion), 0) FROM bienes b 
                 WHERE b.activo = TRUE AND b.tipo_bien = 'devolutivo' {filtroB}) AS ValorPPE,

                (SELECT COALESCE(SUM(b.depreciacion_acumulada), 0) FROM bienes b 
                 WHERE b.activo = TRUE AND b.tipo_bien = 'devolutivo' {filtroB}) AS DepreciacionAcumulada,

                (SELECT COALESCE(SUM(b.valor_neto), 0) FROM bienes b 
                 WHERE b.activo = TRUE AND b.tipo_bien = 'devolutivo' {filtroB}) AS ValorNeto,

                (SELECT COALESCE(SUM(b.valor_stock), 0) FROM bienes b 
                 WHERE b.activo = TRUE AND b.tipo_bien = 'consumo' {filtroB}) AS ValorStockConsumo,

                (SELECT COUNT(*) FROM bienes b 
                 WHERE b.activo = TRUE AND b.funcionario_id IS NULL 
                   AND b.tipo_bien = 'devolutivo' {filtroB}) AS BienesSinResponsable,

                (SELECT COUNT(*) FROM bienes b 
                 WHERE b.activo = TRUE AND b.aula_id IS NULL {filtroB}) AS BienesSinUbicacion,

                (SELECT COUNT(*) FROM bienes b 
                 WHERE b.activo = TRUE AND b.tipo_bien = 'consumo' 
                   AND b.stock_minimo > 0 
                   AND b.stock_actual <= b.stock_minimo {filtroB}) AS BienesBajoStock,

                (SELECT COUNT(*) FROM prestamos p 
                 WHERE p.anulada = FALSE 
                   AND p.estado = 'VENCIDO' {filtroP}) AS PrestamosVencidos,

                (SELECT COUNT(*) FROM garantias g 
                 WHERE g.anulada = FALSE 
                  AND g.fecha_vencimiento BETWEEN CURRENT_DATE AND (CURRENT_DATE + INTERVAL '30 days')
                   {filtroG}) AS GarantiasPorVencer,

                (SELECT COUNT(*) FROM usuarios u 
                 WHERE u.activo = TRUE {filtroU}) AS UsuariosActivos;";

        using var cn = new NpgsqlConnection(_cs);
        var r = await cn.QuerySingleAsync<DashboardKpisDto>(sql, parametros);

        // Valor inventario total
        r.ValorInventario = r.ValorPPE + r.ValorStockConsumo;
        return r;
    }

    // ═══════════════════════════════════════════════════════
    // Bienes por categoría (para bar chart)
    // ═══════════════════════════════════════════════════════
    public async Task<IEnumerable<BienesPorCategoriaDto>> ObtenerBienesPorCategoriaAsync(int top = 10)
    {
        var (filtro, parametros) = ConstruirFiltro("b");
        parametros.Add("Top", top);

        var sql = $@"
            SELECT
                c.id                     AS CategoriaId,
                c.nombre                 AS CategoriaNombre,
                COUNT(b.id)              AS Cantidad,
                COALESCE(SUM(
                    CASE WHEN b.tipo_bien = 'consumo' 
                         THEN b.valor_stock 
                         ELSE b.valor_adquisicion 
                    END
                ), 0) AS ValorTotal
            FROM categorias c
            LEFT JOIN bienes b ON b.categoria_id = c.id AND b.activo = TRUE {filtro}
            WHERE c.estado = TRUE
            GROUP BY c.id, c.nombre
            HAVING COUNT(b.id) > 0
            ORDER BY Cantidad DESC, c.nombre ASC
            LIMIT @Top;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<BienesPorCategoriaDto>(sql, parametros);
    }

    // ═══════════════════════════════════════════════════════
    // Últimos bienes registrados
    // ═══════════════════════════════════════════════════════
    public async Task<IEnumerable<UltimoBienDto>> ObtenerUltimosBienesAsync(int top = 5)
    {
        var (filtro, parametros) = ConstruirFiltro("b");
        parametros.Add("Top", top);

        var sql = $@"
            SELECT
                b.id                              AS Id,
                COALESCE(b.codigo, '—')           AS Codigo,
                b.nombre                          AS Nombre,
                COALESCE(c.nombre, '—')           AS CategoriaNombre,
                COALESCE(
                    CASE WHEN b.tipo_bien = 'consumo' 
                         THEN b.valor_stock 
                         ELSE b.valor_adquisicion 
                    END
                , 0)                              AS Valor,
                b.created_at                      AS FechaRegistro
            FROM bienes b
            LEFT JOIN categorias c ON c.id = b.categoria_id
            WHERE b.activo = TRUE {filtro}
            ORDER BY b.created_at DESC NULLS LAST, b.id DESC
            LIMIT @Top;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<UltimoBienDto>(sql, parametros);
    }

    // ═══════════════════════════════════════════════════════
    // Depreciación mensual (para line chart)
    // ═══════════════════════════════════════════════════════
    public async Task<IEnumerable<DepreciacionMensualDto>> ObtenerDepreciacionMensualAsync(int meses = 12)
    {
        var (filtro, parametros) = ConstruirFiltro("b");

        var sql = $@"
            SELECT
                d.periodo_anio                                        AS Anio,
                d.periodo_mes                                         AS Mes,
                TO_CHAR(TO_DATE(d.periodo_anio || '-' || d.periodo_mes || '-01', 'YYYY-MM-DD'), 'Mon YY') AS Etiqueta,
                COALESCE(SUM(d.depreciacion_mes), 0)                  AS DepreciacionMes,
                COALESCE(SUM(d.depreciacion_acumulada), 0)            AS DepreciacionAcumulada
            FROM depreciacion d
            INNER JOIN bienes b ON b.id = d.bien_id
            WHERE b.activo = TRUE {filtro}
              AND (d.periodo_anio * 100 + d.periodo_mes) >= 
                  (EXTRACT(YEAR FROM CURRENT_DATE - INTERVAL '{meses} months')::int * 100 
                   + EXTRACT(MONTH FROM CURRENT_DATE - INTERVAL '{meses} months')::int)
            GROUP BY d.periodo_anio, d.periodo_mes
            ORDER BY d.periodo_anio ASC, d.periodo_mes ASC;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<DepreciacionMensualDto>(sql, parametros);
    }

    // ═══════════════════════════════════════════════════════
    // Top 5 bienes por valor
    // ═══════════════════════════════════════════════════════
    public async Task<IEnumerable<TopBienDto>> ObtenerTopBienesAsync(int top = 5)
    {
        var (filtro, parametros) = ConstruirFiltro("b");
        parametros.Add("Top", top);

        var sql = $@"
            SELECT
                b.id                              AS Id,
                COALESCE(b.codigo, '—')           AS Codigo,
                b.nombre                          AS Nombre,
                COALESCE(c.nombre, '—')           AS CategoriaNombre,
                b.valor_adquisicion               AS ValorAdquisicion,
                b.valor_neto                      AS ValorNeto,
                b.depreciacion_acumulada          AS DepreciacionAcumulada
            FROM bienes b
            LEFT JOIN categorias c ON c.id = b.categoria_id
            WHERE b.activo = TRUE 
              AND b.tipo_bien = 'devolutivo' {filtro}
            ORDER BY b.valor_adquisicion DESC
            LIMIT @Top;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<TopBienDto>(sql, parametros);
    }
}