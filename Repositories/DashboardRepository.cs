using Almacen.DTOs;
using Almacen.Helpers;
using Almacen.Interfaces;
using Almacen.Services;
using Dapper;
using Npgsql;

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

    public async Task<DashboardKpisDto> ObtenerKpisAsync()
    {
        var (filtroBienes, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "b");
        var (filtroUsers, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "u");

        // ⚠️ NOTA: categorias y roles son GLOBALES → sin filtro
        var sql = $@"
            SELECT
                (SELECT COUNT(*) FROM bienes b WHERE b.activo = TRUE {filtroBienes}) AS TotalBienes,
                (SELECT COUNT(*) FROM categorias WHERE estado = TRUE) AS TotalCategorias,
                (SELECT COUNT(*) FROM usuarios u WHERE u.activo = TRUE {filtroUsers}) AS UsuariosActivos,
                (SELECT COUNT(*) FROM roles) AS TotalRoles,
                (SELECT COALESCE(SUM(
                    CASE 
                        WHEN b.tipo_bien = 'consumo' THEN b.valor_stock
                        ELSE b.valor_adquisicion
                    END
                ), 0) FROM bienes b WHERE b.activo = TRUE {filtroBienes}) AS ValorInventario;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QuerySingleAsync<DashboardKpisDto>(sql,
            new { InstitucionId = _sesion.InstitucionId });
    }

    public async Task<IEnumerable<BienesPorCategoriaDto>> ObtenerBienesPorCategoriaAsync(int top = 5)
    {
        var (filtroBienes, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "b");

        // ⚠️ Filtramos los BIENES (por institución), no las categorías
        var sql = $@"
            SELECT
                c.id AS CategoriaId,
                c.nombre AS CategoriaNombre,
                COUNT(b.id) AS Cantidad,
                COALESCE(SUM(
                    CASE 
                        WHEN b.tipo_bien = 'consumo' THEN b.valor_stock
                        ELSE b.valor_adquisicion
                    END
                ), 0) AS ValorTotal
            FROM categorias c
            LEFT JOIN bienes b ON b.categoria_id = c.id AND b.activo = TRUE {filtroBienes}
            WHERE c.estado = TRUE
            GROUP BY c.id, c.nombre
            ORDER BY Cantidad DESC, c.nombre ASC
            LIMIT @Top;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<BienesPorCategoriaDto>(sql,
            new { Top = top, InstitucionId = _sesion.InstitucionId });
    }

    public async Task<IEnumerable<UltimoBienDto>> ObtenerUltimosBienesAsync(int top = 5)
    {
        var (filtroInst, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "b");

        var sql = $@"
            SELECT
                b.id                              AS Id,
                COALESCE(b.codigo, '—')           AS Codigo,
                b.nombre                          AS Nombre,
                COALESCE(c.nombre, '—')           AS CategoriaNombre,
                COALESCE(
                    CASE 
                        WHEN b.tipo_bien = 'consumo' THEN b.valor_stock
                        ELSE b.valor_adquisicion
                    END
                , 0)                              AS Valor,
                b.created_at                      AS FechaRegistro
            FROM bienes b
            LEFT JOIN categorias c ON c.id = b.categoria_id
            WHERE b.activo = TRUE {filtroInst}
            ORDER BY b.created_at DESC NULLS LAST, b.id DESC
            LIMIT @Top;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<UltimoBienDto>(sql,
            new { Top = top, InstitucionId = _sesion.InstitucionId });
    }
}