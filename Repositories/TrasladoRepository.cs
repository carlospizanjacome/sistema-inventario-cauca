using Almacen.DTOs;
using Almacen.Helpers;
using Almacen.Interfaces;
using Almacen.Services;
using Dapper;
using Npgsql;

namespace Almacen.Repositories;

public class TrasladoRepository : ITrasladoRepository
{
    private readonly string _cs;
    private readonly UsuarioSesionService _sesion;

    public TrasladoRepository(IConfiguration cfg, UsuarioSesionService sesion)
    {
        _cs = cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta DefaultConnection.");
        _sesion = sesion;
    }

    private const string BaseSelect = @"
        SELECT
            t.id,
            t.bien_id                   AS BienId,
            t.aula_origen_id            AS AulaOrigenId,
            t.aula_destino_id           AS AulaDestinoId,
            t.funcionario_anterior_id   AS FuncionarioAnteriorId,
            t.funcionario_nuevo_id      AS FuncionarioNuevoId,
            t.fecha_traslado::timestamp AS FechaTraslado,
            t.motivo,
            t.institucion_id            AS InstitucionId,
            b.codigo                    AS BienCodigo,
            b.nombre                    AS BienNombre,
            ao.nombre                   AS AulaOrigenNombre,
            ad.nombre                   AS AulaDestinoNombre,
            fa.nombre_completo          AS FuncionarioAnteriorNombre,
            fn.nombre_completo          AS FuncionarioNuevoNombre
        FROM traslados t
        LEFT JOIN bienes b        ON b.id = t.bien_id
        LEFT JOIN aulas ao        ON ao.id = t.aula_origen_id
        LEFT JOIN aulas ad        ON ad.id = t.aula_destino_id
        LEFT JOIN funcionarios fa ON fa.id = t.funcionario_anterior_id
        LEFT JOIN funcionarios fn ON fn.id = t.funcionario_nuevo_id";

    public async Task<IEnumerable<TrasladoDTO>> ObtenerTodosAsync()
    {
        var (filtro, param) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "t");

        var sql = $"{BaseSelect} WHERE 1=1 {filtro} ORDER BY t.fecha_traslado DESC, t.id DESC;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<TrasladoDTO>(sql, param);
    }

    public async Task<TrasladoDTO?> ObtenerPorIdAsync(int id)
    {
        var (filtro, param) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "t");

        var sql = $"{BaseSelect} WHERE t.id = @Id {filtro};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<TrasladoDTO>(sql,
            new { Id = id, InstitucionId = _sesion.InstitucionId });
    }

    public async Task<int> CrearAsync(TrasladoDTO dto)
    {
        const string sql = @"
            INSERT INTO traslados
                (bien_id, aula_origen_id, aula_destino_id,
                 funcionario_anterior_id, funcionario_nuevo_id,
                 fecha_traslado, motivo, institucion_id)
            VALUES
                (@BienId, @AulaOrigenId, @AulaDestinoId,
                 @FuncionarioAnteriorId, @FuncionarioNuevoId,
                 @FechaTraslado, @Motivo, @InstitucionId)
            RETURNING id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, new
        {
            dto.BienId,
            dto.AulaOrigenId,
            dto.AulaDestinoId,
            dto.FuncionarioAnteriorId,
            dto.FuncionarioNuevoId,
            dto.FechaTraslado,
            dto.Motivo,
            InstitucionId = _sesion.InstitucionId
        });
    }

    public async Task<bool> EliminarAsync(int id)
    {
        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync("DELETE FROM traslados WHERE id = @Id;",
            new { Id = id }) > 0;
    }

    public async Task<(int? AulaId, int? FuncionarioId)> ObtenerUbicacionActualAsync(int bienId)
    {
        const string sql = @"
            SELECT aula_id AS AulaId, funcionario_id AS FuncionarioId
            FROM bienes WHERE id = @Id;";

        using var cn = new NpgsqlConnection(_cs);
        var r = await cn.QueryFirstOrDefaultAsync<(int?, int?)>(sql, new { Id = bienId });
        return r;
    }

    public async Task ActualizarUbicacionBienAsync(int bienId, int aulaId, int? funcionarioId)
    {
        const string sql = @"
            UPDATE bienes
            SET aula_id = @AulaId,
                funcionario_id = @FuncionarioId,
                updated_at = NOW()
            WHERE id = @BienId;";

        using var cn = new NpgsqlConnection(_cs);
        await cn.ExecuteAsync(sql, new
        {
            BienId = bienId,
            AulaId = aulaId,
            FuncionarioId = funcionarioId
        });
    }

    public async Task<ResultadoPaginado<TrasladoDTO>> ObtenerPaginadoAsync(
        int pagina = 1,
        int tamano = 25,
        string? filtroTexto = null)
    {
        if (pagina < 1) pagina = 1;
        if (tamano < 1) tamano = 25;
        if (tamano > 200) tamano = 200;

        var (filtroInst, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "t");

        var filtroBusqueda = string.Empty;
        if (!string.IsNullOrWhiteSpace(filtroTexto))
        {
            filtroBusqueda = @" AND (b.codigo ILIKE @Buscar 
                                 OR b.nombre ILIKE @Buscar 
                                 OR t.motivo ILIKE @Buscar)";
        }

        var sqlCount = $@"
            SELECT COUNT(*) FROM traslados t
            LEFT JOIN bienes b ON b.id = t.bien_id
            WHERE 1=1 {filtroInst} {filtroBusqueda};";

        var sqlData = $@"
            {BaseSelect}
            WHERE 1=1 {filtroInst} {filtroBusqueda}
            ORDER BY t.fecha_traslado DESC, t.id DESC
            LIMIT @Tamano OFFSET @Offset;";

        using var cn = new NpgsqlConnection(_cs);
        var parametros = new
        {
            InstitucionId = _sesion.InstitucionId,
            Buscar = $"%{filtroTexto}%",
            Tamano = tamano,
            Offset = (pagina - 1) * tamano
        };

        var total = await cn.ExecuteScalarAsync<int>(sqlCount, parametros);
        var items = (await cn.QueryAsync<TrasladoDTO>(sqlData, parametros)).ToList();

        return new ResultadoPaginado<TrasladoDTO>
        {
            Items = items,
            TotalRegistros = total,
            PaginaActual = pagina,
            TamanoPagina = tamano
        };
    }

    public async Task<ResultadoPaginado<TrasladoDTO>> ObtenerPaginadoConFiltrosAsync(
        FiltroMovimientoDTO filtro,
        int pagina = 1,
        int tamano = 25)
    {
        if (pagina < 1) pagina = 1;
        if (tamano < 1) tamano = 25;
        if (tamano > 200) tamano = 200;

        var (filtroInst, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "t");

        var condiciones = new List<string>();
        var parametros = new DynamicParameters();
        parametros.Add("InstitucionId", _sesion.InstitucionId);

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            condiciones.Add(@"(b.codigo ILIKE @Texto OR b.nombre ILIKE @Texto 
                              OR t.motivo ILIKE @Texto)");
            parametros.Add("Texto", $"%{filtro.Texto}%");
        }

        if (filtro.FechaDesde.HasValue)
        {
            condiciones.Add("t.fecha_traslado >= @FechaDesde");
            parametros.Add("FechaDesde", filtro.FechaDesde.Value);
        }

        if (filtro.FechaHasta.HasValue)
        {
            condiciones.Add("t.fecha_traslado <= @FechaHasta");
            parametros.Add("FechaHasta", filtro.FechaHasta.Value);
        }

        var whereExtra = condiciones.Any() ? " AND " + string.Join(" AND ", condiciones) : "";

        parametros.Add("Tamano", tamano);
        parametros.Add("Offset", (pagina - 1) * tamano);

        var sqlCount = $@"
            SELECT COUNT(*) FROM traslados t
            LEFT JOIN bienes b ON b.id = t.bien_id
            WHERE 1=1 {filtroInst} {whereExtra};";

        var sqlData = $@"
            {BaseSelect}
            WHERE 1=1 {filtroInst} {whereExtra}
            ORDER BY t.fecha_traslado DESC, t.id DESC
            LIMIT @Tamano OFFSET @Offset;";

        using var cn = new NpgsqlConnection(_cs);
        var total = await cn.ExecuteScalarAsync<int>(sqlCount, parametros);
        var items = (await cn.QueryAsync<TrasladoDTO>(sqlData, parametros)).ToList();

        return new ResultadoPaginado<TrasladoDTO>
        {
            Items = items,
            TotalRegistros = total,
            PaginaActual = pagina,
            TamanoPagina = tamano
        };
    }
}