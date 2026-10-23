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

    private bool DebeFiltrarPorSede() => !_sesion.VeTodasLasSedes;

    private (string Sql, DynamicParameters Parametros) ConstruirFiltros(string alias)
    {
        var condiciones = new List<string>();
        var parametros = new DynamicParameters();

        if (!_sesion.EsSuperAdmin && _sesion.InstitucionId > 0)
        {
            condiciones.Add($"{alias}.institucion_id = @InstitucionId");
            parametros.Add("InstitucionId", _sesion.InstitucionId);
        }

        if (DebeFiltrarPorSede())
        {
            condiciones.Add($"{alias}.sede_id = @SedeId");
            parametros.Add("SedeId", _sesion.SedeId!.Value);
        }

        var sql = condiciones.Count > 0 ? " AND " + string.Join(" AND ", condiciones) : string.Empty;
        return (sql, parametros);
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
            t.sede_id                   AS SedeId,
            t.anulada,
            t.anulada_por               AS AnuladaPor,
            t.anulada_fecha::timestamp  AS AnuladaFecha,
            t.anulada_motivo            AS AnuladaMotivo,
            b.codigo                    AS BienCodigo,
            b.nombre                    AS BienNombre,
            ao.nombre                   AS AulaOrigenNombre,
            ad.nombre                   AS AulaDestinoNombre,
            fa.nombre_completo          AS FuncionarioAnteriorNombre,
            fn.nombre_completo          AS FuncionarioNuevoNombre,
            sed.nombre                  AS SedeNombre
        FROM traslados t
        LEFT JOIN bienes b        ON b.id = t.bien_id
        LEFT JOIN aulas ao        ON ao.id = t.aula_origen_id
        LEFT JOIN aulas ad        ON ad.id = t.aula_destino_id
        LEFT JOIN funcionarios fa ON fa.id = t.funcionario_anterior_id
        LEFT JOIN funcionarios fn ON fn.id = t.funcionario_nuevo_id
        LEFT JOIN sedes sed       ON sed.id = t.sede_id";

    public async Task<IEnumerable<TrasladoDTO>> ObtenerTodosAsync()
    {
        var (filtro, param) = ConstruirFiltros("t");
        var sql = $"{BaseSelect} WHERE t.anulada = FALSE {filtro} ORDER BY t.fecha_traslado DESC, t.id DESC;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<TrasladoDTO>(sql, param);
    }

    public async Task<TrasladoDTO?> ObtenerPorIdAsync(int id)
    {
        var (filtro, param) = ConstruirFiltros("t");
        param.Add("Id", id);

        var sql = $"{BaseSelect} WHERE t.id = @Id {filtro};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<TrasladoDTO>(sql, param);
    }

    public async Task<int> CrearAsync(TrasladoDTO dto)
    {
        if (!_sesion.EsSuperAdmin)
            dto.InstitucionId = _sesion.InstitucionId;

        if (!_sesion.VeTodasLasSedes)
            dto.SedeId = _sesion.SedeId;

        if (dto.SedeId == null)
            throw new InvalidOperationException("La sede es obligatoria para registrar el traslado.");

        const string sql = @"
            INSERT INTO traslados
                (bien_id, aula_origen_id, aula_destino_id,
                 funcionario_anterior_id, funcionario_nuevo_id,
                 fecha_traslado, motivo, institucion_id, sede_id, anulada)
            VALUES
                (@BienId, @AulaOrigenId, @AulaDestinoId,
                 @FuncionarioAnteriorId, @FuncionarioNuevoId,
                 @FechaTraslado, @Motivo, @InstitucionId, @SedeId, FALSE)
            RETURNING id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, dto);
    }

    public async Task AnularAsync(int id, string motivo)
    {
        using var cn = new NpgsqlConnection(_cs);
        await cn.OpenAsync();
        using var tx = await cn.BeginTransactionAsync();

        try
        {
            var bienId = await cn.ExecuteScalarAsync<int?>(
                "SELECT bien_id FROM traslados WHERE id = @Id AND anulada = FALSE",
                new { Id = id }, tx);

            if (bienId is null)
                throw new InvalidOperationException("El traslado no existe o ya está anulado.");

            var esUltimo = await cn.ExecuteScalarAsync<bool>(@"
                SELECT NOT EXISTS (
                    SELECT 1 FROM traslados
                    WHERE bien_id = @BienId
                      AND anulada = FALSE
                      AND (fecha_traslado, id) > (
                          SELECT fecha_traslado, id FROM traslados WHERE id = @Id
                      )
                );",
                new { BienId = bienId.Value, Id = id }, tx);

            if (!esUltimo)
                throw new InvalidOperationException(
                    "No se puede anular: hay traslados posteriores que dependen de este. Anule primero los posteriores.");

            await cn.ExecuteAsync(@"
                UPDATE traslados
                SET anulada = TRUE,
                    anulada_por = @UsuarioId,
                    anulada_fecha = NOW(),
                    anulada_motivo = @Motivo,
                    updated_at = NOW()
                WHERE id = @Id;",
                new { Id = id, UsuarioId = _sesion.UsuarioActual?.Id, Motivo = motivo }, tx);

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<(int? AulaId, int? FuncionarioId)> ObtenerUbicacionActualAsync(int bienId)
    {
        const string sql = @"SELECT aula_id AS AulaId, funcionario_id AS FuncionarioId FROM bienes WHERE id = @Id;";
        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<(int?, int?)>(sql, new { Id = bienId });
    }

    // ✅ CORREGIDO: Ahora actualiza la sede del bien
    public async Task ActualizarUbicacionBienAsync(int bienId, int? aulaId, int? funcionarioId, int? sedeId)
    {
        const string sql = @"
            UPDATE bienes
            SET aula_id = @AulaId,
                funcionario_id = @FuncionarioId,
                sede_id = @SedeId,
                updated_at = NOW()
            WHERE id = @BienId;";

        using var cn = new NpgsqlConnection(_cs);
        await cn.ExecuteAsync(sql, new
        {
            BienId = bienId,
            AulaId = aulaId,
            FuncionarioId = funcionarioId,
            SedeId = sedeId
        });
    }

    public async Task<ResultadoPaginado<TrasladoDTO>> ObtenerPaginadoAsync(
        int pagina = 1, int tamano = 25, string? filtroTexto = null)
    {
        if (pagina < 1) pagina = 1;
        if (tamano < 1) tamano = 25;
        if (tamano > 200) tamano = 200;

        var (filtroInst, parametros) = ConstruirFiltros("t");
        var condiciones = new List<string> { "t.anulada = FALSE" };

        if (!string.IsNullOrWhiteSpace(filtroTexto))
        {
            condiciones.Add(@"(b.codigo ILIKE @Buscar OR b.nombre ILIKE @Buscar OR t.motivo ILIKE @Buscar)");
            parametros.Add("Buscar", $"%{filtroTexto}%");
        }

        var whereExtra = " AND " + string.Join(" AND ", condiciones);
        parametros.Add("Tamano", tamano);
        parametros.Add("Offset", (pagina - 1) * tamano);

        var sqlCount = $@"SELECT COUNT(*) FROM traslados t LEFT JOIN bienes b ON b.id = t.bien_id WHERE 1=1 {filtroInst} {whereExtra};";
        var sqlData = $@"{BaseSelect} WHERE 1=1 {filtroInst} {whereExtra} ORDER BY t.fecha_traslado DESC, t.id DESC LIMIT @Tamano OFFSET @Offset;";

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

    public async Task<ResultadoPaginado<TrasladoDTO>> ObtenerPaginadoConFiltrosAsync(
        FiltroMovimientoDTO filtro, int pagina = 1, int tamano = 25)
    {
        if (pagina < 1) pagina = 1;
        if (tamano < 1) tamano = 25;
        if (tamano > 200) tamano = 200;

        var (filtroInst, parametros) = ConstruirFiltros("t");
        var condiciones = new List<string> { "t.anulada = FALSE" };

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            condiciones.Add(@"(b.codigo ILIKE @Texto OR b.nombre ILIKE @Texto OR t.motivo ILIKE @Texto)");
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

        var whereExtra = " AND " + string.Join(" AND ", condiciones);
        parametros.Add("Tamano", tamano);
        parametros.Add("Offset", (pagina - 1) * tamano);

        var sqlCount = $@"SELECT COUNT(*) FROM traslados t LEFT JOIN bienes b ON b.id = t.bien_id WHERE 1=1 {filtroInst} {whereExtra};";
        var sqlData = $@"{BaseSelect} WHERE 1=1 {filtroInst} {whereExtra} ORDER BY t.fecha_traslado DESC, t.id DESC LIMIT @Tamano OFFSET @Offset;";

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