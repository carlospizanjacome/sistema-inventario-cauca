using Almacen.DTOs;
using Almacen.Helpers;
using Almacen.Interfaces;
using Almacen.Services;
using Dapper;
using Npgsql;
using System.Text.Json;

namespace Almacen.Repositories;

public class ImportacionRepository : IImportacionRepository
{
    private readonly string _cs;
    private readonly UsuarioSesionService _sesion;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public ImportacionRepository(IConfiguration cfg, UsuarioSesionService sesion)
    {
        _cs = cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta DefaultConnection.");
        _sesion = sesion;
    }

    // ═══════════════════════════════════════════════════════
    // CABECERA
    // ═══════════════════════════════════════════════════════

    private const string BaseSelect = @"
        SELECT
            i.id                    AS Id,
            i.institucion_id        AS InstitucionId,
            i.usuario_id            AS UsuarioId,
            i.nombre_archivo        AS NombreArchivo,
            i.tipo_archivo          AS TipoArchivo,
            i.total_filas           AS TotalFilas,
            i.filas_exitosas        AS FilasExitosas,
            i.filas_con_warning     AS FilasConWarning,
            i.filas_con_error       AS FilasConError,
            i.filas_omitidas        AS FilasOmitidas,
            i.estado,
            i.mapeo_columnas        AS MapeoColumnas,
            i.observaciones,
            i.fecha_inicio          AS FechaInicio,
            i.fecha_fin             AS FechaFin,
            u.nombre_completo       AS UsuarioNombre,
            inst.nombre             AS InstitucionNombre
        FROM importaciones i
        LEFT JOIN usuarios u       ON u.id = i.usuario_id
        LEFT JOIN instituciones inst ON inst.id = i.institucion_id";

    public async Task<ResultadoPaginado<ImportacionDTO>> ObtenerPaginadoAsync(
        int pagina, int tamano, string? filtroEstado = null)
    {
        if (pagina < 1) pagina = 1;
        if (tamano < 1) tamano = 25;
        if (tamano > 200) tamano = 200;

        var (filtroInst, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "i");

        var condiciones = new List<string> { "i.anulada = FALSE" };
        var parametros = new DynamicParameters();
        parametros.Add("InstitucionId", _sesion.InstitucionId);

        if (!string.IsNullOrWhiteSpace(filtroEstado))
        {
            condiciones.Add("i.estado = @Estado");
            parametros.Add("Estado", filtroEstado);
        }

        var whereExtra = " AND " + string.Join(" AND ", condiciones);

        parametros.Add("Tamano", tamano);
        parametros.Add("Offset", (pagina - 1) * tamano);

        var sqlCount = $@"SELECT COUNT(*) FROM importaciones i WHERE 1=1 {filtroInst} {whereExtra};";

        var sqlData = $@"{BaseSelect}
                         WHERE 1=1 {filtroInst} {whereExtra}
                         ORDER BY i.fecha_inicio DESC
                         LIMIT @Tamano OFFSET @Offset;";

        using var cn = new NpgsqlConnection(_cs);
        var total = await cn.ExecuteScalarAsync<int>(sqlCount, parametros);
        var items = (await cn.QueryAsync<ImportacionDTO>(sqlData, parametros)).ToList();

        return new ResultadoPaginado<ImportacionDTO>
        {
            Items = items,
            TotalRegistros = total,
            PaginaActual = pagina,
            TamanoPagina = tamano
        };
    }

    public async Task<ImportacionDTO?> ObtenerPorIdAsync(int id)
    {
        var (filtroInst, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "i");

        var sql = $"{BaseSelect} WHERE i.id = @Id AND i.anulada = FALSE {filtroInst};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<ImportacionDTO>(sql,
            new { Id = id, InstitucionId = _sesion.InstitucionId });
    }

    public async Task<int> CrearAsync(ImportacionDTO dto)
    {
        var usuarioId = _sesion.UsuarioActual?.Id
            ?? throw new InvalidOperationException("No hay usuario en sesión.");

        const string sql = @"
            INSERT INTO importaciones
                (institucion_id, usuario_id, nombre_archivo, tipo_archivo,
                 total_filas, estado, mapeo_columnas, observaciones, fecha_inicio)
            VALUES
                (@InstitucionId, @UsuarioId, @NombreArchivo, @TipoArchivo,
                 @TotalFilas, 'EN_PROCESO', @MapeoColumnas::jsonb, @Observaciones, NOW())
            RETURNING id;";

        dto.InstitucionId = _sesion.InstitucionId;
        dto.UsuarioId = usuarioId;

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, new
        {
            dto.InstitucionId,
            UsuarioId = usuarioId,
            dto.NombreArchivo,
            dto.TipoArchivo,
            dto.TotalFilas,
            MapeoColumnas = dto.MapeoColumnas ?? "{}",
            dto.Observaciones
        });
    }

    public async Task ActualizarEstadoAsync(int id, string nuevoEstado)
    {
        const string sql = @"
            UPDATE importaciones
            SET estado = @Estado,
                fecha_fin = CASE WHEN @Estado IN ('COMPLETADA','COMPLETADA_CON_ERRORES','FALLIDA','CANCELADA')
                                 THEN NOW() ELSE fecha_fin END
            WHERE id = @Id;";

        using var cn = new NpgsqlConnection(_cs);
        await cn.ExecuteAsync(sql, new { Id = id, Estado = nuevoEstado });
    }

    public async Task AnularAsync(int id, string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("El motivo es obligatorio.", nameof(motivo));

        var usuarioId = _sesion.UsuarioActual?.Id
            ?? throw new InvalidOperationException("No hay usuario en sesión.");

        using var cn = new NpgsqlConnection(_cs);
        await cn.OpenAsync();
        using var tx = await cn.BeginTransactionAsync();

        try
        {
            var imp = await cn.QueryFirstOrDefaultAsync<(int Id, bool Anulada, int InstId)>(
                @"SELECT id AS Id, anulada AS Anulada, institucion_id AS InstId
                  FROM importaciones WHERE id = @Id FOR UPDATE;",
                new { Id = id }, tx);

            if (imp.Id == 0)
                throw new InvalidOperationException("La importación no existe.");

            if (!_sesion.EsSuperAdmin && imp.InstId != _sesion.InstitucionId)
                throw new InvalidOperationException("No tiene permiso para anular esta importación.");

            if (imp.Anulada)
                throw new InvalidOperationException("La importación ya está anulada.");

            const string sql = @"
                UPDATE importaciones
                SET anulada = TRUE,
                    anulada_por = @UsuarioId,
                    anulada_fecha = NOW(),
                    anulada_motivo = @Motivo
                WHERE id = @Id;";

            await cn.ExecuteAsync(sql, new
            {
                Id = id,
                UsuarioId = usuarioId,
                Motivo = motivo.Trim()
            }, tx);

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ═══════════════════════════════════════════════════════
    // DETALLE
    // ═══════════════════════════════════════════════════════

    public async Task<IEnumerable<ImportacionDetalleDTO>> ObtenerDetalleAsync(int importacionId)
    {
        const string sql = @"
            SELECT
                d.id                AS Id,
                d.importacion_id    AS ImportacionId,
                d.numero_fila       AS NumeroFila,
                d.datos_crudos      AS DatosCrudos,
                d.estado,
                d.errores,
                d.advertencias,
                d.bien_id           AS BienId,
                b.codigo            AS BienCodigo,
                b.nombre            AS BienNombre
            FROM importacion_detalle d
            LEFT JOIN bienes b ON b.id = d.bien_id
            WHERE d.importacion_id = @ImportacionId
            ORDER BY d.numero_fila;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<ImportacionDetalleDTO>(sql, new { ImportacionId = importacionId });
    }

    public async Task<int> InsertarDetalleAsync(int importacionId, IEnumerable<FilaImportacionDTO> filas)
    {
        using var cn = new NpgsqlConnection(_cs);
        await cn.OpenAsync();
        using var tx = await cn.BeginTransactionAsync();

        try
        {
            const string sql = @"
                INSERT INTO importacion_detalle
                    (importacion_id, numero_fila, datos_crudos, estado, errores, advertencias)
                VALUES
                    (@ImportacionId, @NumeroFila, @DatosCrudos::jsonb, @Estado, @Errores, @Advertencias);";

            var count = 0;
            foreach (var fila in filas)
            {
                var datosJson = JsonSerializer.Serialize(fila.DatosCrudos, JsonOpts);
                var errores = fila.Errores.Any() ? string.Join("; ", fila.Errores) : null;
                var advertencias = fila.Advertencias.Any() ? string.Join("; ", fila.Advertencias) : null;

                await cn.ExecuteAsync(sql, new
                {
                    ImportacionId = importacionId,
                    fila.NumeroFila,
                    DatosCrudos = datosJson,
                    fila.Estado,
                    Errores = errores,
                    Advertencias = advertencias
                }, tx);

                count++;
            }

            await tx.CommitAsync();
            return count;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task ActualizarDetalleImportadoAsync(int detalleId, int bienId)
    {
        const string sql = @"
            UPDATE importacion_detalle
            SET estado = 'IMPORTADO', bien_id = @BienId
            WHERE id = @DetalleId;";

        using var cn = new NpgsqlConnection(_cs);
        await cn.ExecuteAsync(sql, new { DetalleId = detalleId, BienId = bienId });
    }

    // ═══════════════════════════════════════════════════════
    // ✨ NUEVOS MÉTODOS — Para el fix del importador real
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Marca un detalle como IMPORTADO y le asocia el ID del bien creado.
    /// Se usa la clave compuesta (importacion_id + numero_fila) porque
    /// InsertarDetalleAsync no devuelve los IDs generados.
    /// </summary>
    public async Task MarcarDetalleImportadoPorFilaAsync(
        int importacionId, int numeroFila, int bienId)
    {
        const string sql = @"
            UPDATE importacion_detalle
            SET estado = 'IMPORTADO',
                bien_id = @BienId,
                errores = NULL
            WHERE importacion_id = @ImportacionId
              AND numero_fila = @NumeroFila;";

        using var cn = new NpgsqlConnection(_cs);
        await cn.ExecuteAsync(sql, new
        {
            ImportacionId = importacionId,
            NumeroFila = numeroFila,
            BienId = bienId
        });
    }

    /// <summary>
    /// Marca un detalle como ERROR con el motivo.
    /// </summary>
    public async Task MarcarDetalleErrorPorFilaAsync(
        int importacionId, int numeroFila, string error)
    {
        const string sql = @"
            UPDATE importacion_detalle
            SET estado = 'ERROR',
                errores = @Error
            WHERE importacion_id = @ImportacionId
              AND numero_fila = @NumeroFila;";

        using var cn = new NpgsqlConnection(_cs);
        await cn.ExecuteAsync(sql, new
        {
            ImportacionId = importacionId,
            NumeroFila = numeroFila,
            Error = error
        });
    }

    /// <summary>
    /// Actualiza los contadores reales de la cabecera tras la importación.
    /// </summary>
    public async Task ActualizarContadoresAsync(
        int importacionId, int exitosas, int conWarning, int conError)
    {
        const string sql = @"
            UPDATE importaciones
            SET filas_exitosas = @Exitosas,
                filas_con_warning = @ConWarning,
                filas_con_error = @ConError,
                fecha_fin = NOW()
            WHERE id = @Id;";

        using var cn = new NpgsqlConnection(_cs);
        await cn.ExecuteAsync(sql, new
        {
            Id = importacionId,
            Exitosas = exitosas,
            ConWarning = conWarning,
            ConError = conError
        });
    }

    // ═══════════════════════════════════════════════════════
    // MÉTRICAS
    // ═══════════════════════════════════════════════════════

    public async Task<(int Total, int Completadas, int ConError)> ObtenerMetricasAsync()
    {
        var (filtro, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "i");

        var sql = $@"
            SELECT
                COUNT(*)::int AS Total,
                COUNT(*) FILTER (WHERE i.estado LIKE 'COMPLETADA%')::int AS Completadas,
                COUNT(*) FILTER (WHERE i.estado = 'FALLIDA')::int AS ConError
            FROM importaciones i
            WHERE i.anulada = FALSE {filtro};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstAsync<(int Total, int Completadas, int ConError)>(
            sql, new { InstitucionId = _sesion.InstitucionId });
    }
}