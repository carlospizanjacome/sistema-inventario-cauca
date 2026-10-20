using Almacen.DTOs;
using Almacen.Helpers;
using Almacen.Interfaces;
using Almacen.Services;
using Dapper;
using Npgsql;

namespace Almacen.Repositories;

public class SalidaRepository : ISalidaRepository
{
    private readonly string _cs;
    private readonly UsuarioSesionService _sesion;

    public SalidaRepository(IConfiguration cfg, UsuarioSesionService sesion)
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
            s.id,
            s.bien_id              AS BienId,
            s.tipo_baja            AS TipoBaja,
            s.motivo,
            s.fecha_salida::timestamp AS FechaSalida,
            s.numero_acta_comite   AS NumeroActaComite,
            s.numero_denuncia      AS NumeroDenuncia,
            s.valor_salida         AS ValorSalida,
            s.funcionario_aprueba_id AS FuncionarioApruebaId,
            s.observaciones,
            s.institucion_id       AS InstitucionId,
            s.sede_id              AS SedeId,
            s.anulada,
            s.anulada_por          AS AnuladaPor,
            s.anulada_fecha::timestamp AS AnuladaFecha,
            s.anulada_motivo       AS AnuladaMotivo,
            b.codigo               AS BienCodigo,
            b.nombre               AS BienNombre,
            f.nombre_completo      AS FuncionarioNombre,
            sed.nombre             AS SedeNombre
        FROM salidas s
        LEFT JOIN bienes b       ON b.id = s.bien_id
        LEFT JOIN funcionarios f ON f.id = s.funcionario_aprueba_id
        LEFT JOIN sedes sed      ON sed.id = s.sede_id";

    public async Task<IEnumerable<SalidaDTO>> ObtenerTodasAsync()
    {
        var (filtro, param) = ConstruirFiltros("s");
        var sql = $"{BaseSelect} WHERE s.anulada = FALSE {filtro} ORDER BY s.fecha_salida DESC, s.id DESC;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<SalidaDTO>(sql, param);
    }

    public async Task<SalidaDTO?> ObtenerPorIdAsync(int id)
    {
        var (filtro, param) = ConstruirFiltros("s");
        param.Add("Id", id);

        var sql = $"{BaseSelect} WHERE s.id = @Id {filtro};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<SalidaDTO>(sql, param);
    }

    public async Task<int> CrearAsync(SalidaDTO dto)
    {
        if (!_sesion.EsSuperAdmin)
            dto.InstitucionId = _sesion.InstitucionId;

        if (!_sesion.VeTodasLasSedes)
            dto.SedeId = _sesion.SedeId;

        if (dto.SedeId == null)
            throw new InvalidOperationException("La sede es obligatoria para registrar la salida.");

        const string sql = @"
            INSERT INTO salidas
                (bien_id, tipo_baja, motivo, fecha_salida, numero_acta_comite,
                 numero_denuncia, valor_salida, funcionario_aprueba_id, observaciones,
                 institucion_id, sede_id, anulada)
            VALUES
                (@BienId, @TipoBaja, @Motivo, @FechaSalida, @NumeroActaComite,
                 @NumeroDenuncia, @ValorSalida, @FuncionarioApruebaId, @Observaciones,
                 @InstitucionId, @SedeId, FALSE)
            RETURNING id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, dto);
    }

    public async Task<bool> ActualizarAsync(SalidaDTO dto)
    {
        var original = await ObtenerPorIdAsync(dto.Id);
        if (original is null)
            throw new InvalidOperationException("No se encontró la salida a actualizar.");

        if (original.Anulada)
            throw new InvalidOperationException("No se puede editar una salida anulada.");

        var esFormal = !string.IsNullOrWhiteSpace(original.NumeroActaComite)
                    || !string.IsNullOrWhiteSpace(original.NumeroDenuncia);

        if (esFormal)
        {
            const string sqlSoloObservaciones = @"
                UPDATE salidas
                SET observaciones = @Observaciones
                WHERE id = @Id AND anulada = FALSE;";

            using var cn = new NpgsqlConnection(_cs);
            return await cn.ExecuteAsync(sqlSoloObservaciones, new
            {
                dto.Id,
                dto.Observaciones
            }) > 0;
        }

        if (!_sesion.VeTodasLasSedes)
            dto.SedeId = _sesion.SedeId;

        const string sql = @"
            UPDATE salidas
            SET bien_id = @BienId,
                tipo_baja = @TipoBaja,
                motivo = @Motivo,
                fecha_salida = @FechaSalida,
                numero_acta_comite = @NumeroActaComite,
                numero_denuncia = @NumeroDenuncia,
                valor_salida = @ValorSalida,
                funcionario_aprueba_id = @FuncionarioApruebaId,
                observaciones = @Observaciones,
                sede_id = @SedeId
            WHERE id = @Id AND anulada = FALSE;";

        using var cnFull = new NpgsqlConnection(_cs);
        return await cnFull.ExecuteAsync(sql, dto) > 0;
    }

    public async Task AnularAsync(int id, string motivo)
    {
        using var cn = new NpgsqlConnection(_cs);
        await cn.OpenAsync();
        using var tx = await cn.BeginTransactionAsync();

        try
        {
            var salida = await cn.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT bien_id, numero_acta_comite, numero_denuncia, anulada
                  FROM salidas WHERE id = @Id",
                new { Id = id }, tx);

            if (salida is null)
                throw new InvalidOperationException("La salida no existe.");

            if ((bool)salida.anulada)
                throw new InvalidOperationException("La salida ya está anulada.");

            string? acta = salida.numero_acta_comite as string;
            string? denuncia = salida.numero_denuncia as string;

            if (!string.IsNullOrWhiteSpace(acta) || !string.IsNullOrWhiteSpace(denuncia))
            {
                var motivos = new List<string>();
                if (!string.IsNullOrWhiteSpace(acta)) motivos.Add($"Acta '{acta}'");
                if (!string.IsNullOrWhiteSpace(denuncia)) motivos.Add($"Denuncia '{denuncia}'");

                throw new InvalidOperationException(
                    $"No se puede anular una salida formal. Tiene {string.Join(" y ", motivos)}. " +
                    "Use el proceso de 'Anular Baja' administrativo.");
            }

            int bienId = (int)salida.bien_id;

            await cn.ExecuteAsync(@"
                UPDATE salidas
                SET anulada = TRUE,
                    anulada_por = @UsuarioId,
                    anulada_fecha = NOW(),
                    anulada_motivo = @Motivo
                WHERE id = @Id;",
                new
                {
                    Id = id,
                    UsuarioId = _sesion.UsuarioActual?.Id,
                    Motivo = motivo
                }, tx);

            await cn.ExecuteAsync(@"
                UPDATE bienes
                SET activo = TRUE,
                    updated_at = NOW()
                WHERE id = @BienId;",
                new { BienId = bienId }, tx);

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> BienTieneSalidaAsync(int bienId, int? excluirId = null)
    {
        const string sql = @"
            SELECT COUNT(*) FROM salidas
            WHERE bien_id = @BienId
              AND anulada = FALSE
              AND (@ExcluirId IS NULL OR id <> @ExcluirId);";

        using var cn = new NpgsqlConnection(_cs);
        var n = await cn.ExecuteScalarAsync<int>(sql,
            new { BienId = bienId, ExcluirId = excluirId });
        return n > 0;
    }

    public async Task ReactivarBienAsync(int bienId)
    {
        using var cn = new NpgsqlConnection(_cs);
        await cn.ExecuteAsync(
            "UPDATE bienes SET activo = TRUE, updated_at = NOW() WHERE id = @Id;",
            new { Id = bienId });
    }

    public async Task DesactivarBienAsync(int bienId)
    {
        using var cn = new NpgsqlConnection(_cs);
        await cn.ExecuteAsync(
            "UPDATE bienes SET activo = FALSE, updated_at = NOW() WHERE id = @Id;",
            new { Id = bienId });
    }

    public async Task<ResultadoPaginado<SalidaDTO>> ObtenerPaginadoAsync(
        int pagina = 1, int tamano = 25, string? filtroTexto = null)
    {
        if (pagina < 1) pagina = 1;
        if (tamano < 1) tamano = 25;
        if (tamano > 200) tamano = 200;

        var (filtroInst, parametros) = ConstruirFiltros("s");
        var condiciones = new List<string> { "s.anulada = FALSE" };

        if (!string.IsNullOrWhiteSpace(filtroTexto))
        {
            condiciones.Add(@"(b.codigo ILIKE @Buscar OR b.nombre ILIKE @Buscar 
                               OR s.motivo ILIKE @Buscar OR s.numero_acta_comite ILIKE @Buscar)");
            parametros.Add("Buscar", $"%{filtroTexto}%");
        }

        var whereExtra = " AND " + string.Join(" AND ", condiciones);
        parametros.Add("Tamano", tamano);
        parametros.Add("Offset", (pagina - 1) * tamano);

        var sqlCount = $@"SELECT COUNT(*) FROM salidas s LEFT JOIN bienes b ON b.id = s.bien_id WHERE 1=1 {filtroInst} {whereExtra};";
        var sqlData = $@"{BaseSelect} WHERE 1=1 {filtroInst} {whereExtra} ORDER BY s.fecha_salida DESC, s.id DESC LIMIT @Tamano OFFSET @Offset;";

        using var cn = new NpgsqlConnection(_cs);
        var total = await cn.ExecuteScalarAsync<int>(sqlCount, parametros);
        var items = (await cn.QueryAsync<SalidaDTO>(sqlData, parametros)).ToList();

        return new ResultadoPaginado<SalidaDTO>
        {
            Items = items,
            TotalRegistros = total,
            PaginaActual = pagina,
            TamanoPagina = tamano
        };
    }

    public async Task<ResultadoPaginado<SalidaDTO>> ObtenerPaginadoConFiltrosAsync(
        FiltroMovimientoDTO filtro, int pagina = 1, int tamano = 25)
    {
        if (pagina < 1) pagina = 1;
        if (tamano < 1) tamano = 25;
        if (tamano > 200) tamano = 200;

        var (filtroInst, parametros) = ConstruirFiltros("s");
        var condiciones = new List<string> { "s.anulada = FALSE" };

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            condiciones.Add(@"(b.codigo ILIKE @Texto OR b.nombre ILIKE @Texto 
                              OR s.motivo ILIKE @Texto OR s.numero_acta_comite ILIKE @Texto)");
            parametros.Add("Texto", $"%{filtro.Texto}%");
        }

        if (filtro.FechaDesde.HasValue)
        {
            condiciones.Add("s.fecha_salida >= @FechaDesde");
            parametros.Add("FechaDesde", filtro.FechaDesde.Value);
        }

        if (filtro.FechaHasta.HasValue)
        {
            condiciones.Add("s.fecha_salida <= @FechaHasta");
            parametros.Add("FechaHasta", filtro.FechaHasta.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Tipo))
        {
            condiciones.Add("s.tipo_baja = @Tipo");
            parametros.Add("Tipo", filtro.Tipo);
        }

        var whereExtra = " AND " + string.Join(" AND ", condiciones);
        parametros.Add("Tamano", tamano);
        parametros.Add("Offset", (pagina - 1) * tamano);

        var sqlCount = $@"SELECT COUNT(*) FROM salidas s LEFT JOIN bienes b ON b.id = s.bien_id WHERE 1=1 {filtroInst} {whereExtra};";
        var sqlData = $@"{BaseSelect} WHERE 1=1 {filtroInst} {whereExtra} ORDER BY s.fecha_salida DESC, s.id DESC LIMIT @Tamano OFFSET @Offset;";

        using var cn = new NpgsqlConnection(_cs);
        var total = await cn.ExecuteScalarAsync<int>(sqlCount, parametros);
        var items = (await cn.QueryAsync<SalidaDTO>(sqlData, parametros)).ToList();

        return new ResultadoPaginado<SalidaDTO>
        {
            Items = items,
            TotalRegistros = total,
            PaginaActual = pagina,
            TamanoPagina = tamano
        };
    }
}