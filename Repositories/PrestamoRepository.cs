using Almacen.DTOs;
using Almacen.Helpers;
using Almacen.Interfaces;
using Almacen.Services;
using Dapper;
using Npgsql;

namespace Almacen.Repositories;

public class PrestamoRepository : IPrestamoRepository
{
    private readonly string _cs;
    private readonly UsuarioSesionService _sesion;

    public PrestamoRepository(IConfiguration cfg, UsuarioSesionService sesion)
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
            p.id,
            p.bien_id                   AS BienId,
            p.institucion_id            AS InstitucionId,
            p.sede_id                   AS SedeId,
            p.funcionario_solicita_id   AS FuncionarioSolicitaId,
            p.funcionario_aprueba_id    AS FuncionarioApruebaId,

            p.fecha_solicitud::timestamp           AS FechaSolicitud,
            p.fecha_aprobacion::timestamp          AS FechaAprobacion,
            p.fecha_prestamo::timestamp            AS FechaPrestamo,
            p.fecha_devolucion_prevista::timestamp AS FechaDevolucionPrevista,
            p.fecha_devolucion_real::timestamp     AS FechaDevolucionReal,

            p.estado,
            p.motivo,
            p.observaciones_entrega      AS ObservacionesEntrega,
            p.observaciones_devolucion   AS ObservacionesDevolucion,
            p.estado_bien_entrega        AS EstadoBienEntrega,
            p.estado_bien_devolucion     AS EstadoBienDevolucion,

            p.anulada                    AS Anulada,
            p.anulada_por                AS AnuladaPor,
            p.anulada_fecha              AS AnuladaFecha,
            p.anulada_motivo             AS AnuladaMotivo,
            p.anulada_por_email          AS AnuladaPorEmail,
            p.anulada_por_nombre         AS AnuladaPorNombre,

            b.codigo                     AS BienCodigo,
            b.nombre                     AS BienNombre,
            c.nombre                     AS BienCategoriaNombre,

            fs.nombre_completo           AS FuncionarioSolicitaNombre,
            fs.cedula                    AS FuncionarioSolicitaCedula,
            fa.nombre_completo           AS FuncionarioApruebaNombre,

            sed.nombre                   AS SedeNombre
        FROM prestamos p
        LEFT JOIN bienes b            ON b.id = p.bien_id
        LEFT JOIN categorias c        ON c.id = b.categoria_id
        LEFT JOIN funcionarios fs     ON fs.id = p.funcionario_solicita_id
        LEFT JOIN funcionarios fa     ON fa.id = p.funcionario_aprueba_id
        LEFT JOIN sedes sed           ON sed.id = p.sede_id";

    public async Task<ResultadoPaginado<PrestamoDTO>> ObtenerPaginadoAsync(
        int pagina, int tamano,
        string? filtroTexto,
        string? filtroEstado,
        DateTime? filtroFechaDesde,
        DateTime? filtroFechaHasta)
    {
        if (pagina < 1) pagina = 1;
        if (tamano < 1) tamano = 25;
        if (tamano > 200) tamano = 200;

        var (filtroSql, parametros) = ConstruirFiltros("p");

        var condiciones = new List<string> { "p.anulada = FALSE" };

        if (!string.IsNullOrWhiteSpace(filtroTexto))
        {
            condiciones.Add(@"(b.codigo ILIKE @Buscar 
                             OR b.nombre ILIKE @Buscar 
                             OR fs.nombre_completo ILIKE @Buscar 
                             OR p.motivo ILIKE @Buscar)");
            parametros.Add("Buscar", $"%{filtroTexto}%");
        }

        if (!string.IsNullOrWhiteSpace(filtroEstado))
        {
            condiciones.Add("p.estado = @Estado");
            parametros.Add("Estado", filtroEstado);
        }

        if (filtroFechaDesde.HasValue)
        {
            condiciones.Add("p.fecha_solicitud >= @FechaDesde");
            parametros.Add("FechaDesde", filtroFechaDesde.Value.Date);
        }

        if (filtroFechaHasta.HasValue)
        {
            condiciones.Add("p.fecha_solicitud <= @FechaHasta");
            parametros.Add("FechaHasta", filtroFechaHasta.Value.Date);
        }

        var whereExtra = " AND " + string.Join(" AND ", condiciones);

        parametros.Add("Tamano", tamano);
        parametros.Add("Offset", (pagina - 1) * tamano);

        var sqlCount = $@"SELECT COUNT(*) FROM prestamos p
                          LEFT JOIN bienes b ON b.id = p.bien_id
                          LEFT JOIN funcionarios fs ON fs.id = p.funcionario_solicita_id
                          WHERE 1=1 {filtroSql} {whereExtra};";

        var sqlData = $@"{BaseSelect}
                         WHERE 1=1 {filtroSql} {whereExtra}
                         ORDER BY 
                            CASE p.estado 
                                WHEN 'VENCIDO' THEN 1
                                WHEN 'PRESTADO' THEN 2
                                WHEN 'APROBADO' THEN 3
                                WHEN 'SOLICITADO' THEN 4
                                WHEN 'DEVUELTO' THEN 5
                                WHEN 'RECHAZADO' THEN 6
                                WHEN 'ANULADO' THEN 7
                                ELSE 8
                            END,
                            p.fecha_devolucion_prevista ASC,
                            p.id DESC
                         LIMIT @Tamano OFFSET @Offset;";

        using var cn = new NpgsqlConnection(_cs);
        var total = await cn.ExecuteScalarAsync<int>(sqlCount, parametros);
        var items = (await cn.QueryAsync<PrestamoDTO>(sqlData, parametros)).ToList();

        return new ResultadoPaginado<PrestamoDTO>
        {
            Items = items,
            TotalRegistros = total,
            PaginaActual = pagina,
            TamanoPagina = tamano
        };
    }

    public async Task<PrestamoDTO?> ObtenerPorIdAsync(int id)
    {
        var (filtroSql, parametros) = ConstruirFiltros("p");
        parametros.Add("Id", id);

        var sql = $"{BaseSelect} WHERE p.id = @Id AND p.anulada = FALSE {filtroSql};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<PrestamoDTO>(sql, parametros);
    }

    public async Task<int> CrearAsync(PrestamoDTO dto)
    {
        if (!_sesion.EsSuperAdmin)
            dto.InstitucionId = _sesion.InstitucionId;

        if (!_sesion.VeTodasLasSedes)
            dto.SedeId = _sesion.SedeId;

        if (dto.SedeId == null)
            throw new InvalidOperationException("La sede es obligatoria para registrar el préstamo.");

        const string sql = @"
            INSERT INTO prestamos
                (bien_id, institucion_id, sede_id, funcionario_solicita_id,
                 fecha_solicitud, fecha_devolucion_prevista,
                 estado, motivo,
                 created_at, updated_at)
            VALUES
                (@BienId, @InstitucionId, @SedeId, @FuncionarioSolicitaId,
                 @FechaSolicitud, @FechaDevolucionPrevista,
                 'SOLICITADO', @Motivo,
                 NOW(), NOW())
            RETURNING id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, new
        {
            dto.BienId,
            dto.InstitucionId,
            dto.SedeId,
            dto.FuncionarioSolicitaId,
            FechaSolicitud = DateTime.Today,
            dto.FechaDevolucionPrevista,
            dto.Motivo
        });
    }

    public async Task AprobarAsync(int id, int funcionarioApruebaId)
    {
        const string sql = @"
            UPDATE prestamos
            SET estado = 'APROBADO',
                funcionario_aprueba_id = @FuncionarioApruebaId,
                fecha_aprobacion = NOW()::date,
                updated_at = NOW()
            WHERE id = @Id AND estado = 'SOLICITADO' AND anulada = FALSE;";

        using var cn = new NpgsqlConnection(_cs);
        await cn.ExecuteAsync(sql, new { Id = id, FuncionarioApruebaId = funcionarioApruebaId });
    }

    public async Task RechazarAsync(int id, string motivo)
    {
        const string sql = @"
            UPDATE prestamos
            SET estado = 'RECHAZADO',
                observaciones_entrega = @Motivo,
                updated_at = NOW()
            WHERE id = @Id AND estado = 'SOLICITADO' AND anulada = FALSE;";

        using var cn = new NpgsqlConnection(_cs);
        await cn.ExecuteAsync(sql, new { Id = id, Motivo = motivo });
    }

    public async Task EntregarAsync(int id, string estadoBienEntrega, string? observaciones)
    {
        const string sql = @"
            UPDATE prestamos
            SET estado = 'PRESTADO',
                fecha_prestamo = NOW()::date,
                estado_bien_entrega = @EstadoBienEntrega,
                observaciones_entrega = @Observaciones,
                updated_at = NOW()
            WHERE id = @Id AND estado = 'APROBADO' AND anulada = FALSE;";

        using var cn = new NpgsqlConnection(_cs);
        await cn.ExecuteAsync(sql, new
        {
            Id = id,
            EstadoBienEntrega = estadoBienEntrega,
            Observaciones = observaciones
        });
    }

    public async Task DevolverAsync(int id, string estadoBienDevolucion, string? observaciones)
    {
        const string sql = @"
            UPDATE prestamos
            SET estado = 'DEVUELTO',
                fecha_devolucion_real = NOW()::date,
                estado_bien_devolucion = @EstadoBienDevolucion,
                observaciones_devolucion = @Observaciones,
                updated_at = NOW()
            WHERE id = @Id AND estado IN ('PRESTADO', 'VENCIDO') AND anulada = FALSE;";

        using var cn = new NpgsqlConnection(_cs);
        await cn.ExecuteAsync(sql, new
        {
            Id = id,
            EstadoBienDevolucion = estadoBienDevolucion,
            Observaciones = observaciones
        });
    }

    public async Task AnularAsync(int id)
    {
        const string sql = @"
            UPDATE prestamos
            SET estado = 'ANULADO',
                updated_at = NOW()
            WHERE id = @Id AND estado IN ('SOLICITADO', 'APROBADO') AND anulada = FALSE;";

        using var cn = new NpgsqlConnection(_cs);
        await cn.ExecuteAsync(sql, new { Id = id });
    }

    public async Task AnularRegistroAsync(int id, string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("El motivo es obligatorio.", nameof(motivo));

        var u = _sesion.UsuarioActual
            ?? throw new InvalidOperationException("No hay usuario en sesión.");

        using var cn = new NpgsqlConnection(_cs);
        await cn.OpenAsync();
        using var tx = await cn.BeginTransactionAsync();

        try
        {
            var p = await cn.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT id, estado, anulada, institucion_id, sede_id 
                  FROM prestamos WHERE id = @Id FOR UPDATE;",
                new { Id = id }, tx);

            if (p is null)
                throw new InvalidOperationException("El préstamo no existe.");

            int instId = (int)p.institucion_id;
            string estado = (string)p.estado;
            bool anulada = (bool)p.anulada;

            if (!_sesion.EsSuperAdmin && instId != _sesion.InstitucionId)
                throw new InvalidOperationException("No tiene permiso para anular este préstamo.");

            if (DebeFiltrarPorSede() && (int)p.sede_id != _sesion.SedeId)
                throw new InvalidOperationException("No tiene permiso para anular préstamos de otra sede.");

            if (anulada)
                throw new InvalidOperationException("El préstamo ya está anulado.");

            if (estado is not ("DEVUELTO" or "RECHAZADO" or "ANULADO"))
                throw new InvalidOperationException(
                    $"Solo se anulan préstamos en estados terminales. Estado actual: {estado}.");

            const string sql = @"
                UPDATE prestamos
                SET anulada = TRUE,
                    anulada_por = @UsuarioId,
                    anulada_por_email = @Email,
                    anulada_por_nombre = @Nombre,
                    anulada_fecha = NOW(),
                    anulada_motivo = @Motivo
                WHERE id = @Id;";

            await cn.ExecuteAsync(sql, new
            {
                Id = id,
                UsuarioId = u.Id,
                Email = u.Email,
                Nombre = u.NombreCompleto,
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

    public async Task<int> ActualizarVencidosAsync()
    {
        var (filtroSql, parametros) = ConstruirFiltros("prestamos");

        var sql = $@"
            UPDATE prestamos
            SET estado = 'VENCIDO', updated_at = NOW()
            WHERE estado = 'PRESTADO'
              AND fecha_devolucion_prevista < NOW()::date
              AND anulada = FALSE
              {filtroSql};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync(sql, parametros);
    }

    public async Task<(int Activos, int Vencidos, int Devueltos, int Total)> ObtenerMetricasAsync()
    {
        var (filtroSql, parametros) = ConstruirFiltros("p");

        var sql = $@"
        SELECT
            COUNT(*) FILTER (WHERE p.estado IN ('SOLICITADO','APROBADO','PRESTADO','VENCIDO'))::int AS Activos,
            COUNT(*) FILTER (WHERE p.estado = 'VENCIDO')::int AS Vencidos,
            COUNT(*) FILTER (WHERE p.estado = 'DEVUELTO')::int AS Devueltos,
            COUNT(*)::int AS Total
        FROM prestamos p
        WHERE p.anulada = FALSE {filtroSql};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstAsync<(int Activos, int Vencidos, int Devueltos, int Total)>(sql, parametros);
    }
}