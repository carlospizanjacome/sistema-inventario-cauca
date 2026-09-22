using Almacen.DTOs;
using Almacen.Helpers;
using Almacen.Interfaces;
using Almacen.Services;
using Dapper;
using Npgsql;

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
        var (filtroBienes, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "b");
        var (filtroFuncs, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "f");
        var (filtroUsers, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "u");

        var sql = $@"
            SELECT
                (SELECT COUNT(*) FROM instituciones WHERE activo = TRUE) AS TotalInstituciones,
                (SELECT COUNT(*) FROM bienes b WHERE b.activo = TRUE {filtroBienes}) AS TotalBienes,
                (SELECT COUNT(*) FROM bienes b WHERE b.activo = TRUE AND b.tipo_bien = 'devolutivo' {filtroBienes}) AS TotalDevolutivos,
                (SELECT COUNT(*) FROM bienes b WHERE b.activo = TRUE AND b.tipo_bien = 'consumo' {filtroBienes}) AS TotalConsumo,
                (SELECT COUNT(*) FROM funcionarios f WHERE f.activo = TRUE {filtroFuncs}) AS TotalFuncionarios,
                (SELECT COUNT(*) FROM usuarios u WHERE u.activo = TRUE {filtroUsers}) AS TotalUsuarios,
                (SELECT COUNT(*) FROM bienes b WHERE b.activo = TRUE AND b.tipo_bien = 'consumo' AND b.stock_actual <= b.stock_minimo {filtroBienes}) AS BienesBajoStock,
                (SELECT COALESCE(SUM(b.valor_adquisicion), 0) FROM bienes b WHERE b.activo = TRUE {filtroBienes}) AS ValorAdquisicion,
                (SELECT COALESCE(SUM(b.depreciacion_acumulada), 0) FROM bienes b WHERE b.activo = TRUE {filtroBienes}) AS DepreciacionAcumulada,
                (SELECT COALESCE(SUM(b.valor_neto), 0) FROM bienes b WHERE b.activo = TRUE {filtroBienes}) AS ValorNeto;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QuerySingleAsync<ConsolidadoGlobalDTO>(sql,
            new { InstitucionId = _sesion.InstitucionId });
    }

    public async Task<IEnumerable<ConsolidadoInstitucionDTO>> ObtenerConsolidadoInstitucionesAsync()
    {
        var sql = @"
            SELECT
                i.id                    AS InstitucionId,
                i.nombre                AS InstitucionNombre,
                i.nit                   AS InstitucionNit,
                i.codigo_dane           AS CodigoDane,
                (SELECT COUNT(*) FROM bienes b WHERE b.institucion_id = i.id AND b.activo = TRUE) AS TotalBienes,
                (SELECT COUNT(*) FROM bienes b WHERE b.institucion_id = i.id AND b.tipo_bien = 'devolutivo' AND b.activo = TRUE) AS TotalDevolutivos,
                (SELECT COUNT(*) FROM bienes b WHERE b.institucion_id = i.id AND b.tipo_bien = 'consumo' AND b.activo = TRUE) AS TotalConsumo,
                (SELECT COUNT(*) FROM funcionarios f WHERE f.institucion_id = i.id AND f.activo = TRUE) AS TotalFuncionarios,
                (SELECT COUNT(*) FROM usuarios u WHERE u.institucion_id = i.id AND u.activo = TRUE) AS TotalUsuarios,
                (SELECT COUNT(*) FROM sedes s WHERE s.institucion_id = i.id AND s.activo = TRUE) AS TotalSedes,
                (SELECT COUNT(*) FROM bienes b WHERE b.institucion_id = i.id AND b.tipo_bien = 'consumo' AND b.activo = TRUE AND b.stock_actual <= b.stock_minimo) AS BienesBajoStock,
                (SELECT COALESCE(SUM(b.valor_adquisicion), 0) FROM bienes b WHERE b.institucion_id = i.id AND b.activo = TRUE) AS ValorAdquisicion,
                (SELECT COALESCE(SUM(b.depreciacion_acumulada), 0) FROM bienes b WHERE b.institucion_id = i.id AND b.activo = TRUE) AS DepreciacionAcumulada,
                (SELECT COALESCE(SUM(b.valor_neto), 0) FROM bienes b WHERE b.institucion_id = i.id AND b.activo = TRUE) AS ValorNeto
            FROM instituciones i
            WHERE i.activo = TRUE";

        if (!_sesion.EsSuperAdmin)
        {
            sql += " AND i.id = @InstitucionId";
        }

        sql += " ORDER BY i.nombre;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<ConsolidadoInstitucionDTO>(sql,
            new { InstitucionId = _sesion.InstitucionId });
    }
}