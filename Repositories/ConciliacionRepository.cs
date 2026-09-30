using Almacen.DTOs;
using Almacen.Helpers;
using Almacen.Interfaces;
using Almacen.Services;
using Dapper;
using Npgsql;

namespace Almacen.Repositories;

public class ConciliacionRepository : IConciliacionRepository
{
    private readonly string _cs;
    private readonly UsuarioSesionService _sesion;

    public ConciliacionRepository(IConfiguration cfg, UsuarioSesionService sesion)
    {
        _cs = cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta DefaultConnection.");
        _sesion = sesion;
    }

    public async Task<IEnumerable<TomaFisicaCerradaDTO>> ObtenerTomasCerradasAsync()
    {
        var (filtroInst, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "t");

        var sql = $@"
            SELECT
                t.id,
                t.codigo,
                t.nombre,
                t.fecha_inicio::timestamp      AS FechaInicio,
                t.fecha_cierre::timestamp      AS FechaCierre,
                t.total_bienes_snapshot        AS TotalBienes
            FROM tomas_fisicas t
            WHERE t.estado = 'CERRADA'
              {filtroInst}
            ORDER BY t.fecha_cierre DESC NULLS LAST, t.id DESC;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<TomaFisicaCerradaDTO>(sql,
            new { InstitucionId = _sesion.InstitucionId });
    }

    public async Task<ConciliacionGlobalDTO?> ObtenerGlobalAsync(int tomaFisicaId)
    {
        var (filtroInst, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "t");

        var sql = $@"
            SELECT
                t.id                       AS TomaFisicaId,
                t.codigo                   AS TomaCodigo,
                t.nombre                   AS TomaNombre,
                t.fecha_inicio::timestamp  AS FechaInicio,
                t.fecha_cierre::timestamp  AS FechaCierre,

                COUNT(d.id)::int                                                             AS TotalSistema,
                COALESCE(SUM(b.valor_adquisicion), 0)::numeric                               AS ValorSistema,

                COUNT(d.id) FILTER (WHERE d.encontrado = TRUE)::int                          AS TotalFisico,
                COALESCE(SUM(b.valor_adquisicion) FILTER (WHERE d.encontrado = TRUE), 0)::numeric AS ValorFisico,

                COUNT(d.id) FILTER (WHERE d.tipo = 'CONCILIADO')::int                        AS TotalConciliados,
                COUNT(d.id) FILTER (WHERE d.tipo = 'FALTANTE')::int                          AS TotalFaltantes,
                COUNT(d.id) FILTER (WHERE d.tipo = 'SOBRANTE')::int                          AS TotalSobrantes,

                COALESCE(SUM(b.valor_adquisicion) FILTER (WHERE d.tipo = 'CONCILIADO'), 0)::numeric AS ValorConciliados,
                COALESCE(SUM(b.valor_adquisicion) FILTER (WHERE d.tipo = 'FALTANTE'), 0)::numeric   AS ValorFaltantes,
                COALESCE(SUM(b.valor_adquisicion) FILTER (WHERE d.tipo = 'SOBRANTE'), 0)::numeric   AS ValorSobrantes

            FROM tomas_fisicas t
            LEFT JOIN toma_fisica_detalle d ON d.toma_fisica_id = t.id
            LEFT JOIN bienes b              ON b.id = d.bien_id
            WHERE t.id = @Id {filtroInst}
            GROUP BY t.id, t.codigo, t.nombre, t.fecha_inicio, t.fecha_cierre;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<ConciliacionGlobalDTO>(sql,
            new { Id = tomaFisicaId, InstitucionId = _sesion.InstitucionId });
    }

    public async Task<IEnumerable<ConciliacionCategoriaDTO>> ObtenerPorCategoriaAsync(int tomaFisicaId)
    {
        const string sql = @"
            SELECT
                c.id                    AS CategoriaId,
                COALESCE(c.nombre, 'Sin categoría') AS CategoriaNombre,
                c.codigo_cgn            AS CodigoCgn,

                COUNT(d.id)::int                                                            AS CantidadSistema,
                COUNT(d.id) FILTER (WHERE d.encontrado = TRUE)::int                         AS CantidadFisica,

                COALESCE(SUM(b.valor_adquisicion), 0)::numeric                              AS ValorSistema,
                COALESCE(SUM(b.valor_adquisicion) FILTER (WHERE d.encontrado = TRUE), 0)::numeric AS ValorFisico,

                COUNT(d.id) FILTER (WHERE d.tipo = 'CONCILIADO')::int                       AS Conciliados,
                COUNT(d.id) FILTER (WHERE d.tipo = 'FALTANTE')::int                         AS Faltantes,
                COUNT(d.id) FILTER (WHERE d.tipo = 'SOBRANTE')::int                         AS Sobrantes

            FROM toma_fisica_detalle d
            LEFT JOIN bienes b      ON b.id = d.bien_id
            LEFT JOIN categorias c  ON c.id = b.categoria_id
            WHERE d.toma_fisica_id = @Id
            GROUP BY c.id, c.nombre, c.codigo_cgn
            ORDER BY c.codigo_cgn NULLS LAST, c.nombre NULLS LAST;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<ConciliacionCategoriaDTO>(sql, new { Id = tomaFisicaId });
    }

    public async Task<IEnumerable<ConciliacionDetalleDTO>> ObtenerDetalleAsync(int tomaFisicaId)
    {
        const string sql = @"
            SELECT
                d.id                    AS DetalleId,
                d.bien_id               AS BienId,
                d.codigo_snapshot       AS CodigoSnapshot,
                d.nombre_snapshot       AS NombreSnapshot,
                c.nombre                AS CategoriaNombre,
                c.codigo_cgn            AS CodigoCgn,
                d.tipo,
                d.encontrado,
                b.valor_adquisicion     AS ValorAdquisicion,
                d.ubicacion_snapshot    AS UbicacionSnapshot,
                d.ubicacion_real        AS UbicacionReal,
                d.funcionario_snapshot  AS FuncionarioSnapshot,
                f.nombre_completo       AS FuncionarioReal,
                d.estado_fisico_real    AS EstadoFisicoReal,
                d.fecha_escaneo         AS FechaEscaneo,
                d.observaciones
            FROM toma_fisica_detalle d
            LEFT JOIN bienes b       ON b.id = d.bien_id
            LEFT JOIN categorias c   ON c.id = b.categoria_id
            LEFT JOIN funcionarios f ON f.id = d.funcionario_real_id
            WHERE d.toma_fisica_id = @Id
            ORDER BY
                CASE d.tipo
                    WHEN 'FALTANTE'  THEN 1
                    WHEN 'SOBRANTE'  THEN 2
                    WHEN 'PENDIENTE' THEN 3
                    WHEN 'CONCILIADO' THEN 4
                    ELSE 5
                END,
                d.codigo_snapshot;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<ConciliacionDetalleDTO>(sql, new { Id = tomaFisicaId });
    }
}