using Almacen.DTOs;
using Almacen.Helpers;
using Almacen.Interfaces;
using Almacen.Services;
using Dapper;
using Npgsql;

namespace Almacen.Repositories;

public class TomaFisicaRepository : ITomaFisicaRepository
{
    private readonly string _cs;
    private readonly UsuarioSesionService _sesion;

    public TomaFisicaRepository(IConfiguration cfg, UsuarioSesionService sesion)
    {
        _cs = cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta DefaultConnection.");
        _sesion = sesion;
    }

    // ═══════════════════════════════════════════════════════
    // TOMAS FÍSICAS
    // ═══════════════════════════════════════════════════════

    private const string BaseToma = @"
        SELECT t.id, t.codigo, t.nombre, t.institucion_id AS InstitucionId,
               t.fecha_inicio::timestamp AS FechaInicio,
               t.fecha_cierre::timestamp AS FechaCierre,
               t.estado, t.responsable_id AS ResponsableId,
               t.total_bienes_snapshot AS TotalBienesSnapshot,
               t.total_encontrados AS TotalEncontrados,
               t.total_faltantes AS TotalFaltantes,
               t.total_sobrantes AS TotalSobrantes,
               t.observaciones, t.created_at AS CreatedAt,
               t.started_at AS StartedAt, t.closed_at AS ClosedAt,
               i.nombre AS InstitucionNombre,
               f.nombre_completo AS ResponsableNombre
        FROM tomas_fisicas t
        LEFT JOIN instituciones i ON i.id = t.institucion_id
        LEFT JOIN funcionarios f ON f.id = t.responsable_id";

    public async Task<IEnumerable<TomaFisicaDTO>> ObtenerTodasAsync()
    {
        var (filtro, param) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "t");

        var sql = $"{BaseToma} WHERE 1=1 {filtro} ORDER BY t.created_at DESC;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<TomaFisicaDTO>(sql, param);
    }

    public async Task<TomaFisicaDTO?> ObtenerPorIdAsync(int id)
    {
        var (filtro, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "t");

        var sql = $"{BaseToma} WHERE t.id = @Id {filtro};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<TomaFisicaDTO>(sql,
            new { Id = id, InstitucionId = _sesion.InstitucionId });
    }

    public async Task<int> CrearAsync(TomaFisicaDTO dto)
    {
        const string sql = @"
            INSERT INTO tomas_fisicas
                (codigo, nombre, institucion_id, fecha_inicio, estado,
                 responsable_id, observaciones)
            VALUES
                (@Codigo, @Nombre, @InstitucionId, @FechaInicio, 'BORRADOR',
                 @ResponsableId, @Observaciones)
            RETURNING id;";

        dto.InstitucionId = _sesion.InstitucionId;

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, dto);
    }

    public async Task<bool> ActualizarAsync(TomaFisicaDTO dto)
    {
        const string sql = @"
            UPDATE tomas_fisicas
            SET nombre = @Nombre,
                fecha_inicio = @FechaInicio,
                responsable_id = @ResponsableId,
                observaciones = @Observaciones
            WHERE id = @Id AND estado = 'BORRADOR';";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync(sql, dto) > 0;
    }

    public async Task<bool> EliminarAsync(int id)
    {
        const string sql = @"
            DELETE FROM tomas_fisicas
            WHERE id = @Id AND estado IN ('BORRADOR', 'ANULADA');";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync(sql, new { Id = id }) > 0;
    }

    public async Task<bool> ExisteCodigoAsync(string codigo, int? excluirId = null)
    {
        var sql = @"
            SELECT COUNT(*) FROM tomas_fisicas
            WHERE codigo = @Codigo
              AND institucion_id = @InstitucionId
              AND (@ExcluirId IS NULL OR id <> @ExcluirId);";

        using var cn = new NpgsqlConnection(_cs);
        var n = await cn.ExecuteScalarAsync<int>(sql, new
        {
            Codigo = codigo,
            InstitucionId = _sesion.InstitucionId,
            ExcluirId = excluirId
        });
        return n > 0;
    }

    // ═══════════════════════════════════════════════════════
    // CICLO DE VIDA — ABRIR / CERRAR
    // ═══════════════════════════════════════════════════════

    public async Task<int> AbrirAsync(int tomaId)
    {
        using var cn = new NpgsqlConnection(_cs);
        await cn.OpenAsync();
        using var tx = await cn.BeginTransactionAsync();

        try
        {
            // 1. Verificar que la toma está en BORRADOR
            var estado = await cn.ExecuteScalarAsync<string>(
                "SELECT estado FROM tomas_fisicas WHERE id = @Id FOR UPDATE;",
                new { Id = tomaId }, tx);

            if (estado is null)
                throw new InvalidOperationException("La toma no existe.");
            if (estado != "BORRADOR")
                throw new InvalidOperationException($"La toma ya está en estado '{estado}'.");

            // 2. Obtener institución de la toma
            var instId = await cn.ExecuteScalarAsync<int>(
                "SELECT institucion_id FROM tomas_fisicas WHERE id = @Id;",
                new { Id = tomaId }, tx);

            // 3. CONGELAR: copiar todos los bienes activos al detalle
            const string sqlSnapshot = @"
                INSERT INTO toma_fisica_detalle
                    (toma_fisica_id, bien_id, codigo_snapshot, nombre_snapshot,
                     ubicacion_snapshot, funcionario_snapshot, tipo, encontrado)
                SELECT
                    @TomaId,
                    b.id,
                    b.codigo,
                    b.nombre,
                    COALESCE(a.nombre, b.ubicacion, '—'),
                    COALESCE(f.nombre_completo, b.responsable, '—'),
                    'PENDIENTE',
                    FALSE
                FROM bienes b
                LEFT JOIN aulas a ON a.id = b.aula_id
                LEFT JOIN funcionarios f ON f.id = b.funcionario_id
                WHERE b.institucion_id = @InstId
                  AND b.activo = TRUE;";

            var count = await cn.ExecuteAsync(sqlSnapshot,
                new { TomaId = tomaId, InstId = instId }, tx);

            // 4. Actualizar cabecera
            const string sqlUpdate = @"
                UPDATE tomas_fisicas
                SET estado = 'EN_CURSO',
                    started_at = NOW(),
                    total_bienes_snapshot = @Count,
                    total_encontrados = 0,
                    total_faltantes = 0,
                    total_sobrantes = 0
                WHERE id = @TomaId;";

            await cn.ExecuteAsync(sqlUpdate, new { TomaId = tomaId, Count = count }, tx);

            await tx.CommitAsync();
            return count;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> CerrarAsync(int tomaId)
    {
        using var cn = new NpgsqlConnection(_cs);
        await cn.OpenAsync();
        using var tx = await cn.BeginTransactionAsync();

        try
        {
            var estado = await cn.ExecuteScalarAsync<string>(
                "SELECT estado FROM tomas_fisicas WHERE id = @Id FOR UPDATE;",
                new { Id = tomaId }, tx);

            if (estado != "EN_CURSO")
                throw new InvalidOperationException($"La toma está en estado '{estado}'. Solo se puede cerrar una en curso.");

            // 1. Marcar faltantes: los PENDIENTES pasan a FALTANTE
            const string sqlFaltantes = @"
                UPDATE toma_fisica_detalle
                SET tipo = 'FALTANTE'
                WHERE toma_fisica_id = @TomaId AND tipo = 'PENDIENTE';";

            await cn.ExecuteAsync(sqlFaltantes, new { TomaId = tomaId }, tx);

            // 2. Calcular totales
            const string sqlTotales = @"
                SELECT
                    SUM(CASE WHEN tipo = 'CONCILIADO' THEN 1 ELSE 0 END) AS Conciliados,
                    SUM(CASE WHEN tipo = 'FALTANTE'   THEN 1 ELSE 0 END) AS Faltantes,
                    SUM(CASE WHEN tipo = 'SOBRANTE'   THEN 1 ELSE 0 END) AS Sobrantes
                FROM toma_fisica_detalle
                WHERE toma_fisica_id = @TomaId;";

            var t = await cn.QuerySingleAsync<(int? Conciliados, int? Faltantes, int? Sobrantes)>(
                sqlTotales, new { TomaId = tomaId }, tx);

            // 3. Actualizar cabecera
            const string sqlUpdate = @"
                UPDATE tomas_fisicas
                SET estado = 'CERRADA',
                    fecha_cierre = CURRENT_DATE,
                    closed_at = NOW(),
                    total_encontrados = @Conciliados,
                    total_faltantes = @Faltantes,
                    total_sobrantes = @Sobrantes
                WHERE id = @TomaId;";

            await cn.ExecuteAsync(sqlUpdate, new
            {
                TomaId = tomaId,
                Conciliados = t.Conciliados ?? 0,
                Faltantes = t.Faltantes ?? 0,
                Sobrantes = t.Sobrantes ?? 0
            }, tx);

            await tx.CommitAsync();
            return true;
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

    private const string BaseDetalle = @"
        SELECT d.id, d.toma_fisica_id AS TomaFisicaId,
               d.bien_id AS BienId,
               d.codigo_snapshot AS CodigoSnapshot,
               d.nombre_snapshot AS NombreSnapshot,
               d.ubicacion_snapshot AS UbicacionSnapshot,
               d.funcionario_snapshot AS FuncionarioSnapshot,
               d.tipo, d.encontrado,
               d.ubicacion_real AS UbicacionReal,
               d.funcionario_real_id AS FuncionarioRealId,
               d.estado_fisico_real AS EstadoFisicoReal,
               d.escaneado_por AS EscaneadoPor,
               d.fecha_escaneo AS FechaEscaneo,
               d.observaciones,
               f.nombre_completo AS FuncionarioRealNombre,
               u.nombre_completo AS UsuarioEscaneoNombre
        FROM toma_fisica_detalle d
        LEFT JOIN funcionarios f ON f.id = d.funcionario_real_id
        LEFT JOIN usuarios u ON u.id = d.escaneado_por";

    public async Task<IEnumerable<TomaFisicaDetalleDTO>> ObtenerDetalleAsync(int tomaId)
    {
        var sql = $"{BaseDetalle} WHERE d.toma_fisica_id = @TomaId ORDER BY d.nombre_snapshot;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<TomaFisicaDetalleDTO>(sql, new { TomaId = tomaId });
    }

    public async Task<TomaFisicaDetalleDTO?> ObtenerDetallePorIdAsync(int detalleId)
    {
        var sql = $"{BaseDetalle} WHERE d.id = @Id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<TomaFisicaDetalleDTO>(sql, new { Id = detalleId });
    }

    public async Task<TomaFisicaDetalleDTO?> BuscarPorCodigoAsync(int tomaId, string codigo)
    {
        var sql = $"{BaseDetalle} WHERE d.toma_fisica_id = @TomaId AND d.codigo_snapshot = @Codigo;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<TomaFisicaDetalleDTO>(sql,
            new { TomaId = tomaId, Codigo = codigo });
    }

    // ═══════════════════════════════════════════════════════
    // ESCANEO — CORAZÓN DEL MÓDULO
    // ═══════════════════════════════════════════════════════

    public async Task<(bool Ok, string Mensaje, string Tipo)> EscanearAsync(
        int tomaId, EscaneoDTO dto, int usuarioId)
    {
        using var cn = new NpgsqlConnection(_cs);

        // 1. Verificar que la toma está EN_CURSO
        var estado = await cn.ExecuteScalarAsync<string>(
            "SELECT estado FROM tomas_fisicas WHERE id = @Id;",
            new { Id = tomaId });

        if (estado is null)
            return (false, "Toma no encontrada.", "");
        if (estado != "EN_CURSO")
            return (false, $"La toma está en estado '{estado}'. No se puede escanear.", "");

        // 2. Extraer código (si viene como QR con formato "INV-XXXX", limpiar)
        var codigo = dto.CodigoQr?.Trim() ?? string.Empty;
        if (codigo.StartsWith("INV-", StringComparison.OrdinalIgnoreCase))
            codigo = codigo.Substring(4);

        // 3. Buscar el bien en el detalle (snapshot)
        var detalle = await cn.QueryFirstOrDefaultAsync<(int Id, string Tipo)>(
            @"SELECT id AS Id, tipo AS Tipo 
              FROM toma_fisica_detalle
              WHERE toma_fisica_id = @TomaId AND codigo_snapshot = @Codigo;",
            new { TomaId = tomaId, Codigo = codigo });

        if (detalle.Id > 0)
        {
            // BIEN ENCONTRADO
            if (detalle.Tipo == "CONCILIADO")
                return (true, $"✓ '{codigo}' ya había sido escaneado antes.", "CONCILIADO");

            const string sqlUpdate = @"
                UPDATE toma_fisica_detalle
                SET tipo = 'CONCILIADO',
                    encontrado = TRUE,
                    ubicacion_real = @UbicacionReal,
                    funcionario_real_id = @FuncionarioRealId,
                    estado_fisico_real = @EstadoFisicoReal,
                    escaneado_por = @UsuarioId,
                    fecha_escaneo = NOW(),
                    observaciones = @Observaciones
                WHERE id = @Id;";

            await cn.ExecuteAsync(sqlUpdate, new
            {
                Id = detalle.Id,
                dto.UbicacionReal,
                dto.FuncionarioRealId,
                dto.EstadoFisicoReal,
                Observaciones = dto.Observaciones,
                UsuarioId = usuarioId
            });

            // Actualizar contador de encontrados
            await cn.ExecuteAsync(@"
                UPDATE tomas_fisicas
                SET total_encontrados = (
                    SELECT COUNT(*) FROM toma_fisica_detalle
                    WHERE toma_fisica_id = @TomaId AND tipo = 'CONCILIADO'
                )
                WHERE id = @TomaId;",
                new { TomaId = tomaId });

            return (true, $"✓ '{codigo}' conciliado correctamente.", "CONCILIADO");
        }

        // 4. No está en el snapshot → ¿existe como bien en BD?
        var bienExistente = await cn.QueryFirstOrDefaultAsync<(int Id, string Codigo, string Nombre)>(
            @"SELECT id AS Id, codigo AS Codigo, nombre AS Nombre
              FROM bienes
              WHERE codigo = @Codigo AND institucion_id = @InstId AND activo = TRUE;",
            new
            {
                Codigo = codigo,
                InstId = _sesion.InstitucionId
            });

        if (bienExistente.Id > 0)
        {
            // SOBRANTE: el bien existe en BD pero NO estaba en el snapshot
            const string sqlInsertSobrante = @"
                INSERT INTO toma_fisica_detalle
                    (toma_fisica_id, bien_id, codigo_snapshot, nombre_snapshot,
                     tipo, encontrado, ubicacion_real, funcionario_real_id,
                     estado_fisico_real, escaneado_por, fecha_escaneo, observaciones)
                VALUES
                    (@TomaId, @BienId, @Codigo, @Nombre,
                     'SOBRANTE', TRUE, @UbicacionReal, @FuncionarioRealId,
                     @EstadoFisicoReal, @UsuarioId, NOW(), @Observaciones);";

            await cn.ExecuteAsync(sqlInsertSobrante, new
            {
                TomaId = tomaId,
                BienId = bienExistente.Id,
                Codigo = bienExistente.Codigo,
                Nombre = bienExistente.Nombre,
                dto.UbicacionReal,
                dto.FuncionarioRealId,
                dto.EstadoFisicoReal,
                Observaciones = dto.Observaciones,
                UsuarioId = usuarioId
            });

            // Actualizar contador de sobrantes
            await cn.ExecuteAsync(@"
                UPDATE tomas_fisicas
                SET total_sobrantes = (
                    SELECT COUNT(*) FROM toma_fisica_detalle
                    WHERE toma_fisica_id = @TomaId AND tipo = 'SOBRANTE'
                )
                WHERE id = @TomaId;",
                new { TomaId = tomaId });

            return (true, $"⚠ '{codigo}' es un SOBRANTE (no estaba en el snapshot).", "SOBRANTE");
        }

        // 5. No existe ni como bien en BD → SOBRANTE total (nunca registrado)
        const string sqlInsertDesconocido = @"
            INSERT INTO toma_fisica_detalle
                (toma_fisica_id, codigo_snapshot, nombre_snapshot,
                 tipo, encontrado, ubicacion_real, funcionario_real_id,
                 estado_fisico_real, escaneado_por, fecha_escaneo, observaciones)
            VALUES
                (@TomaId, @Codigo, 'Bien no registrado en el sistema',
                 'SOBRANTE', TRUE, @UbicacionReal, @FuncionarioRealId,
                 @EstadoFisicoReal, @UsuarioId, NOW(), @Observaciones);";

        await cn.ExecuteAsync(sqlInsertDesconocido, new
        {
            TomaId = tomaId,
            Codigo = codigo,
            dto.UbicacionReal,
            dto.FuncionarioRealId,
            dto.EstadoFisicoReal,
            Observaciones = dto.Observaciones,
            UsuarioId = usuarioId
        });

        await cn.ExecuteAsync(@"
            UPDATE tomas_fisicas
            SET total_sobrantes = (
                SELECT COUNT(*) FROM toma_fisica_detalle
                WHERE toma_fisica_id = @TomaId AND tipo = 'SOBRANTE'
            )
            WHERE id = @TomaId;",
            new { TomaId = tomaId });

        return (true, $"⚠ '{codigo}' no existe en el sistema. Registrado como SOBRANTE.", "SOBRANTE");
    }

    // ═══════════════════════════════════════════════════════
    // REPORTE
    // ═══════════════════════════════════════════════════════

    public async Task<ReporteTomaFisicaDTO?> ObtenerReporteAsync(int tomaId)
    {
        var toma = await ObtenerPorIdAsync(tomaId);
        if (toma is null) return null;

        var detalles = (await ObtenerDetalleAsync(tomaId)).ToList();

        var conciliados = detalles.Where(d => d.Tipo == "CONCILIADO").ToList();
        var faltantes = detalles.Where(d => d.Tipo == "FALTANTE").ToList();
        var sobrantes = detalles.Where(d => d.Tipo == "SOBRANTE").ToList();
        var pendientes = detalles.Where(d => d.Tipo == "PENDIENTE").ToList();

        var totalEsperados = conciliados.Count + faltantes.Count + pendientes.Count;
        var porcentaje = totalEsperados > 0
            ? Math.Round((decimal)conciliados.Count / totalEsperados * 100, 2)
            : 0;

        return new ReporteTomaFisicaDTO
        {
            TomaId = toma.Id,
            Codigo = toma.Codigo,
            Nombre = toma.Nombre,
            InstitucionNombre = toma.InstitucionNombre ?? "",
            FechaInicio = toma.FechaInicio,
            FechaCierre = toma.FechaCierre,
            Estado = toma.Estado,
            ResponsableNombre = toma.ResponsableNombre,

            TotalBienesSnapshot = toma.TotalBienesSnapshot,
            TotalConciliados = conciliados.Count,
            TotalFaltantes = faltantes.Count,
            TotalSobrantes = sobrantes.Count,
            TotalPendientes = pendientes.Count,
            PorcentajeConciliacion = porcentaje,

            DetallesConciliados = conciliados,
            DetallesFaltantes = faltantes,
            DetallesSobrantes = sobrantes
        };
    }
    public async Task<bool> RequiereConfirmacionSobranteAsync(int tomaId, string codigo)
    {
        using var cn = new NpgsqlConnection(_cs);

        var codigoLimpio = (codigo ?? string.Empty).Trim();
        if (codigoLimpio.StartsWith("INV-", StringComparison.OrdinalIgnoreCase))
            codigoLimpio = codigoLimpio.Substring(4);

        if (string.IsNullOrWhiteSpace(codigoLimpio))
            return false;

        var enSnapshot = await cn.ExecuteScalarAsync<int>(
            @"SELECT COUNT(*) FROM toma_fisica_detalle
          WHERE toma_fisica_id = @TomaId AND codigo_snapshot = @Codigo;",
            new { TomaId = tomaId, Codigo = codigoLimpio });

        return enSnapshot == 0;
    }
}