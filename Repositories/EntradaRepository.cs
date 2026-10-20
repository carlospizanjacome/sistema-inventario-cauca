using Almacen.DTOs;
using Almacen.Helpers;
using Almacen.Interfaces;
using Almacen.Services;
using Dapper;
using Npgsql;

namespace Almacen.Repositories;

public class EntradaRepository : IEntradaRepository
{
    private readonly string _cs;
    private readonly UsuarioSesionService _sesion;

    public EntradaRepository(IConfiguration cfg, UsuarioSesionService sesion)
    {
        _cs = cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta DefaultConnection.");
        _sesion = sesion;
    }

    // ═══════════════════════════════════════════════════════
    // REGLA DE NEGOCIO: MULTI-SEDE
    // ═══════════════════════════════════════════════════════
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
            e.id,
            e.bien_id              AS BienId,
            e.tipo_fuente          AS TipoFuente,
            e.numero_factura       AS NumeroFactura,
            e.fecha_entrada::timestamp AS FechaEntrada,
            e.valor,
            e.proveedor,
            e.proveedor_id         AS ProveedorId,
            e.funcionario_recibe_id AS FuncionarioRecibeId,
            e.observaciones,
            e.institucion_id       AS InstitucionId,
            e.sede_id              AS SedeId,
            e.anulada,
            e.anulada_por          AS AnuladaPor,
            e.anulada_fecha::timestamp AS AnuladaFecha,
            e.anulada_motivo       AS AnuladaMotivo,
            b.codigo               AS BienCodigo,
            b.nombre               AS BienNombre,
            f.nombre_completo      AS FuncionarioNombre,
            p.nombre               AS ProveedorNombre,
            s.nombre               AS SedeNombre
        FROM entradas e
        LEFT JOIN bienes b       ON b.id = e.bien_id
        LEFT JOIN funcionarios f ON f.id = e.funcionario_recibe_id
        LEFT JOIN proveedores p  ON p.id = e.proveedor_id
        LEFT JOIN sedes s        ON s.id = e.sede_id";

    public async Task<IEnumerable<EntradaDTO>> ObtenerTodasAsync()
    {
        var (filtro, param) = ConstruirFiltros("e");
        var sql = $"{BaseSelect} WHERE e.anulada = FALSE {filtro} ORDER BY e.fecha_entrada DESC, e.id DESC;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<EntradaDTO>(sql, param);
    }

    public async Task<EntradaDTO?> ObtenerPorIdAsync(int id)
    {
        var (filtro, param) = ConstruirFiltros("e");
        param.Add("Id", id);

        var sql = $"{BaseSelect} WHERE e.id = @Id {filtro};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<EntradaDTO>(sql, param);
    }

    public async Task<int> CrearAsync(EntradaDTO dto)
    {
        if (!_sesion.EsSuperAdmin)
        {
            dto.InstitucionId = _sesion.InstitucionId;
        }

        if (!_sesion.VeTodasLasSedes)
        {
            dto.SedeId = _sesion.SedeId;
        }

        if (dto.SedeId == null)
        {
            throw new InvalidOperationException("La sede es obligatoria para registrar la entrada.");
        }

        const string sql = @"
            INSERT INTO entradas
                (bien_id, tipo_fuente, numero_factura, fecha_entrada, valor,
                 proveedor, proveedor_id, funcionario_recibe_id, observaciones,
                 institucion_id, sede_id, anulada)
            VALUES
                (@BienId, @TipoFuente, @NumeroFactura, @FechaEntrada, @Valor,
                 @Proveedor, @ProveedorId, @FuncionarioRecibeId, @Observaciones,
                 @InstitucionId, @SedeId, FALSE)
            RETURNING id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, dto);
    }

    public async Task<bool> ActualizarAsync(EntradaDTO dto)
    {
        if (!_sesion.VeTodasLasSedes)
        {
            dto.SedeId = _sesion.SedeId;
        }

        const string sql = @"
            UPDATE entradas
            SET bien_id = @BienId,
                tipo_fuente = @TipoFuente,
                numero_factura = @NumeroFactura,
                fecha_entrada = @FechaEntrada,
                valor = @Valor,
                proveedor = @Proveedor,
                proveedor_id = @ProveedorId,
                funcionario_recibe_id = @FuncionarioRecibeId,
                observaciones = @Observaciones,
                sede_id = @SedeId,
                updated_at = NOW()
            WHERE id = @Id AND anulada = FALSE;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync(sql, dto) > 0;
    }

    public async Task AnularAsync(int id, string motivo)
    {
        using var cn = new NpgsqlConnection(_cs);
        await cn.OpenAsync();
        using var tx = await cn.BeginTransactionAsync();

        try
        {
            var entrada = await cn.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT bien_id, fecha_entrada::timestamp AS fecha_entrada, anulada
                  FROM entradas 
                  WHERE id = @Id",
                new { Id = id }, tx);

            if (entrada is null)
                throw new InvalidOperationException("La entrada no existe.");

            if ((bool)entrada.anulada)
                throw new InvalidOperationException("La entrada ya está anulada.");

            int bienId = (int)entrada.bien_id;
            DateTime fechaEntrada = (DateTime)entrada.fecha_entrada;

            var hayEntradasPosteriores = await cn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM entradas
                WHERE bien_id = @BienId
                  AND anulada = FALSE
                  AND id <> @Id
                  AND (fecha_entrada > @FechaEntrada
                       OR (fecha_entrada = @FechaEntrada AND id > @Id));",
                new { BienId = bienId, Id = id, FechaEntrada = fechaEntrada.Date }, tx);

            if (hayEntradasPosteriores > 0)
                throw new InvalidOperationException(
                    $"No se puede anular: hay {hayEntradasPosteriores} entrada(s) posterior(es). " +
                    "Anule primero las entradas más recientes.");

            var hayMovimientosPosteriores = await cn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM movimientos_consumo
                WHERE bien_id = @BienId
                  AND anulada = FALSE
                  AND fecha_movimiento > @FechaEntrada;",
                new { BienId = bienId, FechaEntrada = fechaEntrada.Date }, tx);

            if (hayMovimientosPosteriores > 0)
                throw new InvalidOperationException(
                    $"No se puede anular: hay {hayMovimientosPosteriores} movimiento(s) de consumo posterior(es). " +
                    "Anule primero los movimientos más recientes.");

            await cn.ExecuteAsync(@"
                UPDATE entradas
                SET anulada = TRUE,
                    anulada_por = @UsuarioId,
                    anulada_fecha = NOW(),
                    anulada_motivo = @Motivo,
                    updated_at = NOW()
                WHERE id = @Id;",
                new { Id = id, UsuarioId = _sesion.UsuarioActual?.Id, Motivo = motivo }, tx);

            await RecalcularKardexAsync(bienId, cn, tx);
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private async Task RecalcularKardexAsync(int bienId, NpgsqlConnection cn, NpgsqlTransaction tx)
    {
        await cn.ExecuteAsync(@"
            UPDATE bienes
            SET stock_actual = (
                    SELECT COALESCE(SUM(
                        CASE 
                            WHEN tipo_movimiento IN ('ENTRADA','AJUSTE_POS') THEN cantidad
                            WHEN tipo_movimiento IN ('SALIDA','AJUSTE_NEG') THEN -cantidad
                            ELSE 0
                        END
                    ), 0)
                    FROM movimientos_consumo
                    WHERE bien_id = @BienId AND anulada = FALSE
                ),
                updated_at = NOW()
            WHERE id = @BienId
              AND tipo_bien = 'consumo';",
            new { BienId = bienId }, tx);
    }

    public async Task<bool> EsUltimaEntradaAsync(int id)
    {
        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<bool>(@"
            SELECT NOT EXISTS (
                SELECT 1 FROM entradas e2
                WHERE e2.bien_id = (SELECT bien_id FROM entradas WHERE id = @Id)
                  AND e2.anulada = FALSE AND e2.id <> @Id
                  AND (e2.fecha_entrada > (SELECT fecha_entrada FROM entradas WHERE id = @Id)
                       OR (e2.fecha_entrada = (SELECT fecha_entrada FROM entradas WHERE id = @Id) AND e2.id > @Id))
            );", new { Id = id });
    }

    public async Task<bool> BienTieneEntradaAsync(int bienId, int? excluirId = null)
    {
        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM entradas
            WHERE bien_id = @BienId AND anulada = FALSE
              AND (@ExcluirId IS NULL OR id <> @ExcluirId);",
            new { BienId = bienId, ExcluirId = excluirId }) > 0;
    }

    public async Task<ResultadoPaginado<EntradaDTO>> ObtenerPaginadoAsync(
        int pagina = 1, int tamano = 25, string? filtroTexto = null)
    {
        if (pagina < 1) pagina = 1;
        if (tamano < 1) tamano = 25;
        if (tamano > 200) tamano = 200;

        var (filtro, parametros) = ConstruirFiltros("e");
        var condiciones = new List<string> { "e.anulada = FALSE" };

        if (!string.IsNullOrWhiteSpace(filtroTexto))
        {
            condiciones.Add(@"(b.codigo ILIKE @Buscar OR b.nombre ILIKE @Buscar 
                               OR e.numero_factura ILIKE @Buscar OR e.proveedor ILIKE @Buscar)");
            parametros.Add("Buscar", $"%{filtroTexto}%");
        }

        var whereExtra = " AND " + string.Join(" AND ", condiciones);
        parametros.Add("Tamano", tamano);
        parametros.Add("Offset", (pagina - 1) * tamano);

        var sqlCount = $@"SELECT COUNT(*) FROM entradas e LEFT JOIN bienes b ON b.id = e.bien_id WHERE 1=1 {filtro} {whereExtra};";
        var sqlData = $@"{BaseSelect} WHERE 1=1 {filtro} {whereExtra} ORDER BY e.fecha_entrada DESC, e.id DESC LIMIT @Tamano OFFSET @Offset;";

        using var cn = new NpgsqlConnection(_cs);
        var total = await cn.ExecuteScalarAsync<int>(sqlCount, parametros);
        var items = (await cn.QueryAsync<EntradaDTO>(sqlData, parametros)).ToList();

        return new ResultadoPaginado<EntradaDTO>
        {
            Items = items,
            TotalRegistros = total,
            PaginaActual = pagina,
            TamanoPagina = tamano
        };
    }

    public async Task<ResultadoPaginado<EntradaDTO>> ObtenerPaginadoConFiltrosAsync(
        FiltroMovimientoDTO filtro, int pagina = 1, int tamano = 25)
    {
        if (pagina < 1) pagina = 1;
        if (tamano < 1) tamano = 25;
        if (tamano > 200) tamano = 200;

        var (filtroSql, parametros) = ConstruirFiltros("e");
        var condiciones = new List<string> { "e.anulada = FALSE" };

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            condiciones.Add(@"(b.codigo ILIKE @Texto OR b.nombre ILIKE @Texto 
                              OR e.numero_factura ILIKE @Texto OR e.proveedor ILIKE @Texto)");
            parametros.Add("Texto", $"%{filtro.Texto}%");
        }

        if (filtro.FechaDesde.HasValue)
        {
            condiciones.Add("e.fecha_entrada >= @FechaDesde");
            parametros.Add("FechaDesde", filtro.FechaDesde.Value.Date);
        }

        if (filtro.FechaHasta.HasValue)
        {
            condiciones.Add("e.fecha_entrada <= @FechaHasta");
            parametros.Add("FechaHasta", filtro.FechaHasta.Value.Date);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Tipo))
        {
            condiciones.Add("e.tipo_fuente = @Tipo");
            parametros.Add("Tipo", filtro.Tipo);
        }

        var whereExtra = " AND " + string.Join(" AND ", condiciones);
        parametros.Add("Tamano", tamano);
        parametros.Add("Offset", (pagina - 1) * tamano);

        var sqlCount = $@"SELECT COUNT(*) FROM entradas e LEFT JOIN bienes b ON b.id = e.bien_id WHERE 1=1 {filtroSql} {whereExtra};";
        var sqlData = $@"{BaseSelect} WHERE 1=1 {filtroSql} {whereExtra} ORDER BY e.fecha_entrada DESC, e.id DESC LIMIT @Tamano OFFSET @Offset;";

        using var cn = new NpgsqlConnection(_cs);
        var total = await cn.ExecuteScalarAsync<int>(sqlCount, parametros);
        var items = (await cn.QueryAsync<EntradaDTO>(sqlData, parametros)).ToList();

        return new ResultadoPaginado<EntradaDTO>
        {
            Items = items,
            TotalRegistros = total,
            PaginaActual = pagina,
            TamanoPagina = tamano
        };
    }
}