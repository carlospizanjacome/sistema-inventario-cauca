using Almacen.DTOs;
using Almacen.Helpers;
using Almacen.Interfaces;
using Almacen.Services;
using Dapper;
using Npgsql;
using System.Data;

namespace Almacen.Repositories;

public class ReporteRepository : IReporteRepository
{
    private readonly string _cs;
    private readonly UsuarioSesionService _sesion;

    public ReporteRepository(IConfiguration cfg, UsuarioSesionService sesion)
    {
        _cs = cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta DefaultConnection.");
        _sesion = sesion;
    }

    public async Task<ConsolidadoGlobalDTO> ObtenerConsolidadoGlobalAsync()
    {
        var (filtroBienes, _) = FiltroInstitucion.Construir(_sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "b");
        var (filtroFuncs, _) = FiltroInstitucion.Construir(_sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "f");
        var (filtroUsers, _) = FiltroInstitucion.Construir(_sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "u");

        var filtroInstituciones = _sesion.EsSuperAdmin ? string.Empty : " AND id = @InstitucionId";

        var parametros = new DynamicParameters();
        parametros.Add("InstitucionId", _sesion.InstitucionId);

        if (_sesion.SedeId.HasValue)
        {
            filtroBienes += " AND b.sede_id = @SedeId";
            filtroFuncs += " AND f.sede_id = @SedeId";
            filtroUsers += " AND u.sede_id = @SedeId";
            parametros.Add("SedeId", _sesion.SedeId.Value, DbType.Int32);
        }

        var sql = $@"
            SELECT
                (SELECT COUNT(*) FROM instituciones WHERE activo = TRUE {filtroInstituciones}) AS TotalInstituciones,
                (SELECT COUNT(*) FROM bienes b WHERE b.activo = TRUE {filtroBienes}) AS TotalBienes,
                (SELECT COUNT(*) FROM bienes b WHERE b.activo = TRUE AND b.tipo_bien = 'devolutivo' {filtroBienes}) AS TotalDevolutivos,
                (SELECT COUNT(*) FROM bienes b WHERE b.activo = TRUE AND b.tipo_bien = 'consumo' {filtroBienes}) AS TotalConsumo,
                (SELECT COUNT(*) FROM funcionarios f WHERE f.activo = TRUE {filtroFuncs}) AS TotalFuncionarios,
                (SELECT COUNT(*) FROM usuarios u WHERE u.activo = TRUE {filtroUsers}) AS TotalUsuarios,
                (SELECT COUNT(*) FROM bienes b WHERE b.activo = TRUE AND b.tipo_bien = 'consumo' AND b.stock_minimo > 0 AND b.stock_actual <= b.stock_minimo {filtroBienes}) AS BienesBajoStock,
                (SELECT COALESCE(SUM(b.valor_adquisicion), 0) FROM bienes b WHERE b.activo = TRUE AND b.tipo_bien = 'devolutivo' {filtroBienes}) AS ValorAdquisicion,
                (SELECT COALESCE(SUM(b.depreciacion_acumulada), 0) FROM bienes b WHERE b.activo = TRUE AND b.tipo_bien = 'devolutivo' {filtroBienes}) AS DepreciacionAcumulada,
                (SELECT COALESCE(SUM(b.valor_neto), 0) FROM bienes b WHERE b.activo = TRUE AND b.tipo_bien = 'devolutivo' {filtroBienes}) AS ValorNeto,
                (SELECT COALESCE(SUM(b.valor_stock), 0) FROM bienes b WHERE b.activo = TRUE AND b.tipo_bien = 'consumo' {filtroBienes}) AS ValorStockConsumo;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QuerySingleAsync<ConsolidadoGlobalDTO>(sql, parametros);
    }

    public async Task<IEnumerable<ConsolidadoInstitucionDTO>> ObtenerConsolidadoInstitucionesAsync()
    {
        var parametros = new DynamicParameters();
        parametros.Add("InstitucionId", _sesion.InstitucionId);

        var sql = $@"
            SELECT
                i.id                    AS InstitucionId,
                i.nombre                AS InstitucionNombre,
                i.nit                   AS InstitucionNit,
                i.codigo_dane           AS CodigoDane,
                s.id                    AS SedeId,
                s.nombre                AS SedeNombre,
                (SELECT COUNT(*) FROM bienes b WHERE b.sede_id = s.id AND b.activo = TRUE) AS TotalBienes,
                (SELECT COUNT(*) FROM bienes b WHERE b.sede_id = s.id AND b.tipo_bien = 'devolutivo' AND b.activo = TRUE) AS TotalDevolutivos,
                (SELECT COUNT(*) FROM bienes b WHERE b.sede_id = s.id AND b.tipo_bien = 'consumo' AND b.activo = TRUE) AS TotalConsumo,
                (SELECT COUNT(*) FROM funcionarios f WHERE f.sede_id = s.id AND f.activo = TRUE) AS TotalFuncionarios,
                (SELECT COUNT(*) FROM usuarios u WHERE u.sede_id = s.id AND u.activo = TRUE) AS TotalUsuarios,
                (SELECT COUNT(*) FROM sedes s2 WHERE s2.id = s.id) AS TotalSedes,
                (SELECT COUNT(*) FROM bienes b WHERE b.sede_id = s.id AND b.tipo_bien = 'consumo' AND b.activo = TRUE AND b.stock_minimo > 0 AND b.stock_actual <= b.stock_minimo) AS BienesBajoStock,
                (SELECT COALESCE(SUM(b.valor_adquisicion), 0) FROM bienes b WHERE b.sede_id = s.id AND b.activo = TRUE AND b.tipo_bien = 'devolutivo') AS ValorAdquisicion,
                (SELECT COALESCE(SUM(b.depreciacion_acumulada), 0) FROM bienes b WHERE b.sede_id = s.id AND b.activo = TRUE AND b.tipo_bien = 'devolutivo') AS DepreciacionAcumulada,
                (SELECT COALESCE(SUM(b.valor_neto), 0) FROM bienes b WHERE b.sede_id = s.id AND b.activo = TRUE AND b.tipo_bien = 'devolutivo') AS ValorNeto,
                (SELECT COALESCE(SUM(b.valor_stock), 0) FROM bienes b WHERE b.sede_id = s.id AND b.activo = TRUE AND b.tipo_bien = 'consumo') AS ValorStockConsumo
            FROM sedes s
            INNER JOIN instituciones i ON i.id = s.institucion_id
            WHERE s.activo = TRUE AND i.activo = TRUE";

        if (!_sesion.EsSuperAdmin)
        {
            sql += " AND i.id = @InstitucionId";
        }

        if (_sesion.SedeId.HasValue)
        {
            sql += " AND s.id = @SedeId";
            parametros.Add("SedeId", _sesion.SedeId.Value, DbType.Int32);
        }

        sql += " ORDER BY s.nombre;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<ConsolidadoInstitucionDTO>(sql, parametros);
    }
}