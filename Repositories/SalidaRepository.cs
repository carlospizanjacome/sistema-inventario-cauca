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
            s.anulada,
            s.anulada_por          AS AnuladaPor,
            s.anulada_fecha::timestamp AS AnuladaFecha,
            s.anulada_motivo       AS AnuladaMotivo,
            b.codigo               AS BienCodigo,
            b.nombre               AS BienNombre,
            f.nombre_completo      AS FuncionarioNombre
        FROM salidas s
        LEFT JOIN bienes b       ON b.id = s.bien_id
        LEFT JOIN funcionarios f ON f.id = s.funcionario_aprueba_id";

    public async Task<IEnumerable<SalidaDTO>> ObtenerTodasAsync()
    {
        var (filtro, param) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "s");

        var sql = $"{BaseSelect} WHERE s.anulada = FALSE {filtro} ORDER BY s.fecha_salida DESC, s.id DESC;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<SalidaDTO>(sql, param);
    }

    public async Task<SalidaDTO?> ObtenerPorIdAsync(int id)
    {
        var (filtro, param) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "s");

        var sql = $"{BaseSelect} WHERE s.id = @Id {filtro};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<SalidaDTO>(sql,
            new { Id = id, InstitucionId = _sesion.InstitucionId });
    }

    public async Task<int> CrearAsync(SalidaDTO dto)
    {
        const string sql = @"
            INSERT INTO salidas
                (bien_id, tipo_baja, motivo, fecha_salida, numero_acta_comite,
                 numero_denuncia, valor_salida, funcionario_aprueba_id, observaciones,
                 institucion_id, anulada)
            VALUES
                (@BienId, @TipoBaja, @Motivo, @FechaSalida, @NumeroActaComite,
                 @NumeroDenuncia, @ValorSalida, @FuncionarioApruebaId, @Observaciones,
                 @InstitucionId, FALSE)
            RETURNING id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, new
        {
            dto.BienId,
            dto.TipoBaja,
            dto.Motivo,
            dto.FechaSalida,
            dto.NumeroActaComite,
            dto.NumeroDenuncia,
            dto.ValorSalida,
            dto.FuncionarioApruebaId,
            dto.Observaciones,
            InstitucionId = _sesion.InstitucionId
        });
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
                observaciones = @Observaciones
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
        int pagina = 1,
        int tamano = 25,
        string? filtroTexto = null)
    {
        if (pagina < 1) pagina = 1;
        if (tamano < 1) tamano = 25;
        if (tamano > 200) tamano = 200;

        var (filtroInst, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "s");

        var filtroBusqueda = string.Empty;
        if (!string.IsNullOrWhiteSpace(filtroTexto))
        {
            filtroBusqueda = @" AND (b.codigo ILIKE @Buscar 
                                 OR b.nombre ILIKE @Buscar 
                                 OR s.motivo ILIKE @Buscar 
                                 OR s.numero_acta_comite ILIKE @Buscar)";
        }

        var sqlCount = $@"
            SELECT COUNT(*) FROM salidas s
            LEFT JOIN bienes b ON b.id = s.bien_id
            WHERE s.anulada = FALSE {filtroInst} {filtroBusqueda};";

        var sqlData = $@"
            {BaseSelect}
            WHERE s.anulada = FALSE {filtroInst} {filtroBusqueda}
            ORDER BY s.fecha_salida DESC, s.id DESC
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
        FiltroMovimientoDTO filtro,
        int pagina = 1,
        int tamano = 25)
    {
        if (pagina < 1) pagina = 1;
        if (tamano < 1) tamano = 25;
        if (tamano > 200) tamano = 200;

        var (filtroInst, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "s");

        var condiciones = new List<string> { "s.anulada = FALSE" };
        var parametros = new DynamicParameters();
        parametros.Add("InstitucionId", _sesion.InstitucionId);

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

        var whereExtra = string.Join(" AND ", condiciones);

        parametros.Add("Tamano", tamano);
        parametros.Add("Offset", (pagina - 1) * tamano);

        var sqlCount = $@"
            SELECT COUNT(*) FROM salidas s
            LEFT JOIN bienes b ON b.id = s.bien_id
            WHERE {whereExtra} {filtroInst};";

        var sqlData = $@"
            {BaseSelect}
            WHERE {whereExtra} {filtroInst}
            ORDER BY s.fecha_salida DESC, s.id DESC
            LIMIT @Tamano OFFSET @Offset;";

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