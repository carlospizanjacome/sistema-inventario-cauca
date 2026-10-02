using Dapper;
using Npgsql;
using Almacen.DTOs;
using Almacen.Helpers;
using Almacen.Interfaces;
using Almacen.Models;
using Almacen.Services;

namespace Almacen.Repositories;

public class AuditoriaRepository : IAuditoriaRepository
{
    private readonly string _cs;
    private readonly UsuarioSesionService _sesion;

    public AuditoriaRepository(IConfiguration cfg, UsuarioSesionService sesion)
    {
        _cs = cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta DefaultConnection.");
        _sesion = sesion;
    }

    public async Task<long> InsertarAsync(Auditoria a)
    {
        const string sql = @"
            INSERT INTO auditoria (
                institucion_id, usuario_id, usuario_email, usuario_nombre,
                fecha_hora, operacion, modulo, severidad,
                objeto_tipo, objeto_id, objeto_codigo, objeto_descripcion,
                valor_antes, valor_despues, motivo, documento_referencia,
                ip, user_agent
            ) VALUES (
                @InstitucionId, @UsuarioId, @UsuarioEmail, @UsuarioNombre,
                @FechaHora, @Operacion, @Modulo, @Severidad,
                @ObjetoTipo, @ObjetoId, @ObjetoCodigo, @ObjetoDescripcion,
                @ValorAntes, @ValorDespues, @Motivo, @DocumentoReferencia,
                @Ip, @UserAgent
            ) RETURNING id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<long>(sql, a);
    }

    public async Task<AuditoriaPaginaDTO> ObtenerPaginaAsync(AuditoriaFiltro f)
    {
        var (filtro, param) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "a");

        var where = ConstruirWhere(f, filtro);

        var sqlCount = $"SELECT COUNT(*)::int FROM auditoria a WHERE 1=1 {where};";
        var sqlData = $@"
            SELECT
                a.id,
                a.fecha_hora       AS FechaHora,
                a.usuario_nombre   AS UsuarioNombre,
                a.usuario_email    AS UsuarioEmail,
                a.operacion,
                a.modulo,
                a.severidad,
                a.objeto_tipo      AS ObjetoTipo,
                a.objeto_id        AS ObjetoId,
                a.objeto_codigo    AS ObjetoCodigo,
                a.objeto_descripcion AS ObjetoDescripcion,
                a.motivo,
                a.documento_referencia AS DocumentoReferencia,
                a.valor_antes      AS ValorAntes,
                a.valor_despues    AS ValorDespues,
                a.ip
            FROM auditoria a
            WHERE 1=1 {where}
            ORDER BY a.fecha_hora DESC, a.id DESC
            OFFSET @Offset ROWS FETCH NEXT @Tamano ROWS ONLY;";

        var p = ClonarParametros(param, f);
        p.Add("Offset", (f.Pagina - 1) * f.Tamano);
        p.Add("Tamano", f.Tamano);

        using var cn = new NpgsqlConnection(_cs);
        var total = await cn.QueryFirstAsync<int>(sqlCount, p);
        var items = (await cn.QueryAsync<AuditoriaItemDTO>(sqlData, p)).ToList();

        return new AuditoriaPaginaDTO
        {
            Items = items,
            TotalRegistros = total,
            PaginaActual = f.Pagina,
            Tamano = f.Tamano
        };
    }

    public async Task<AuditoriaResumenDTO> ObtenerResumenAsync()
    {
        var (filtro, param) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "a");

        var sql = $@"
        SELECT
            COUNT(*) FILTER (WHERE a.fecha_hora::date = CURRENT_DATE)::int AS eventos_hoy,
            COUNT(*) FILTER (WHERE a.fecha_hora >= CURRENT_DATE - INTERVAL '7 days')::int AS eventos_semana,
            COUNT(*) FILTER (WHERE a.fecha_hora >= CURRENT_DATE - INTERVAL '30 days')::int AS eventos_mes,
            COUNT(*) FILTER (WHERE a.severidad = 'CRITICO'
                               AND a.fecha_hora >= CURRENT_DATE - INTERVAL '30 days')::int AS eventos_criticos,
            COUNT(*)::int AS total_registros
        FROM auditoria a
        WHERE 1=1 {filtro};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstAsync<AuditoriaResumenDTO>(sql, param);
    }

    public async Task<IEnumerable<AuditoriaUsuarioOpcionDTO>> ObtenerUsuariosAsync()
    {
        var (filtro, param) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "a");

        var sql = $@"
            SELECT DISTINCT
                a.usuario_id AS Id,
                COALESCE(a.usuario_nombre, a.usuario_email, 'Desconocido') AS Nombre,
                COALESCE(a.usuario_email, '') AS Email
            FROM auditoria a
            WHERE a.usuario_id IS NOT NULL {filtro}
            ORDER BY 2;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<AuditoriaUsuarioOpcionDTO>(sql, param);
    }

    public async Task<IEnumerable<AuditoriaItemDTO>> ObtenerTodosParaExportarAsync(AuditoriaFiltro f)
    {
        f.Pagina = 1;
        f.Tamano = 10000;

        var pagina = await ObtenerPaginaAsync(f);
        return pagina.Items;
    }

    // ─────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────
    private static string ConstruirWhere(AuditoriaFiltro f, string filtroInstitucion)
    {
        var sb = new System.Text.StringBuilder(filtroInstitucion);

        if (f.FechaDesde.HasValue) sb.Append(" AND a.fecha_hora >= @FechaDesde");
        if (f.FechaHasta.HasValue) sb.Append(" AND a.fecha_hora < (@FechaHasta::date + INTERVAL '1 day')");
        if (!string.IsNullOrWhiteSpace(f.Modulo)) sb.Append(" AND a.modulo = @Modulo");
        if (!string.IsNullOrWhiteSpace(f.Operacion)) sb.Append(" AND a.operacion = @Operacion");
        if (!string.IsNullOrWhiteSpace(f.Severidad)) sb.Append(" AND a.severidad = @Severidad");
        if (f.UsuarioId.HasValue) sb.Append(" AND a.usuario_id = @UsuarioId");
        if (!string.IsNullOrWhiteSpace(f.Busqueda))
            sb.Append(" AND (a.objeto_codigo ILIKE @Bus OR a.objeto_descripcion ILIKE @Bus OR a.motivo ILIKE @Bus)");

        return sb.ToString();
    }

    private static DynamicParameters ClonarParametros(object? original, AuditoriaFiltro f)
    {
        var p = new DynamicParameters();

        if (original is not null)
        {
            foreach (var prop in original.GetType().GetProperties())
            {
                var val = prop.GetValue(original);
                if (val is not null) p.Add(prop.Name, val);
            }
        }

        if (f.FechaDesde.HasValue) p.Add("FechaDesde", f.FechaDesde.Value);
        if (f.FechaHasta.HasValue) p.Add("FechaHasta", f.FechaHasta.Value);
        if (!string.IsNullOrWhiteSpace(f.Modulo)) p.Add("Modulo", f.Modulo);
        if (!string.IsNullOrWhiteSpace(f.Operacion)) p.Add("Operacion", f.Operacion);
        if (!string.IsNullOrWhiteSpace(f.Severidad)) p.Add("Severidad", f.Severidad);
        if (f.UsuarioId.HasValue) p.Add("UsuarioId", f.UsuarioId.Value);
        if (!string.IsNullOrWhiteSpace(f.Busqueda)) p.Add("Bus", $"%{f.Busqueda}%");

        return p;
    }
}