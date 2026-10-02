using Almacen.DTOs;
using Almacen.Helpers;
using Almacen.Interfaces;
using Almacen.Services;
using Dapper;
using Npgsql;

namespace Almacen.Repositories;

public class GarantiaRepository : IGarantiaRepository
{
    private readonly string _cs;
    private readonly UsuarioSesionService _sesion;

    public GarantiaRepository(IConfiguration cfg, UsuarioSesionService sesion)
    {
        _cs = cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta DefaultConnection.");
        _sesion = sesion;
    }

    private const string BaseSelect = @"
        SELECT
            g.id,
            g.bien_id                 AS BienId,
            g.institucion_id          AS InstitucionId,
            g.proveedor_id            AS ProveedorId,
            g.numero_garantia         AS NumeroGarantia,
            g.tipo,
            g.fecha_inicio::timestamp       AS FechaInicio,
            g.fecha_vencimiento::timestamp  AS FechaVencimiento,
            g.condiciones,
            g.contacto_proveedor      AS ContactoProveedor,
            g.observaciones,
            g.anulada,
            g.anulada_por             AS AnuladaPor,
            g.anulada_fecha::timestamp AS AnuladaFecha,
            g.anulada_motivo          AS AnuladaMotivo,
            g.created_at              AS CreatedAt,
            g.updated_at              AS UpdatedAt,
            b.codigo                  AS BienCodigo,
            b.nombre                  AS BienNombre,
            c.nombre                  AS BienCategoriaNombre,
            p.nombre                  AS ProveedorNombre,
            p.nit                     AS ProveedorNit
        FROM garantias g
        LEFT JOIN bienes b       ON b.id = g.bien_id
        LEFT JOIN categorias c   ON c.id = b.categoria_id
        LEFT JOIN proveedores p  ON p.id = g.proveedor_id";

    public async Task<ResultadoPaginado<GarantiaDTO>> ObtenerPaginadoAsync(
        int pagina, int tamano,
        string? filtroTexto,
        string? filtroEstado,
        string? filtroTipo)
    {
        if (pagina < 1) pagina = 1;
        if (tamano < 1) tamano = 25;
        if (tamano > 200) tamano = 200;

        var (filtroInst, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "g");

        // Regla: las vistas operativas solo muestran garantías vigentes (no anuladas)
        var condiciones = new List<string> { "g.anulada = FALSE" };
        var parametros = new DynamicParameters();
        parametros.Add("InstitucionId", _sesion.InstitucionId);

        if (!string.IsNullOrWhiteSpace(filtroTexto))
        {
            condiciones.Add(@"(b.codigo ILIKE @Buscar 
                             OR b.nombre ILIKE @Buscar 
                             OR g.numero_garantia ILIKE @Buscar 
                             OR g.contacto_proveedor ILIKE @Buscar)");
            parametros.Add("Buscar", $"%{filtroTexto}%");
        }

        if (!string.IsNullOrWhiteSpace(filtroEstado))
        {
            switch (filtroEstado.ToUpper())
            {
                case "VIGENTE":
                    condiciones.Add("g.fecha_vencimiento >= NOW()::date + INTERVAL '31 days'");
                    break;
                case "POR_VENCER":
                    condiciones.Add("g.fecha_vencimiento >= NOW()::date");
                    condiciones.Add("g.fecha_vencimiento <= NOW()::date + INTERVAL '30 days'");
                    break;
                case "VENCIDA":
                    condiciones.Add("g.fecha_vencimiento < NOW()::date");
                    break;
            }
        }

        if (!string.IsNullOrWhiteSpace(filtroTipo))
        {
            condiciones.Add("g.tipo = @Tipo");
            parametros.Add("Tipo", filtroTipo);
        }

        var whereExtra = string.Join(" AND ", condiciones);

        parametros.Add("Tamano", tamano);
        parametros.Add("Offset", (pagina - 1) * tamano);

        var sqlCount = $@"SELECT COUNT(*) FROM garantias g
                          LEFT JOIN bienes b ON b.id = g.bien_id
                          WHERE {whereExtra} {filtroInst};";

        var sqlData = $@"{BaseSelect}
                         WHERE {whereExtra} {filtroInst}
                         ORDER BY g.fecha_vencimiento ASC, g.id DESC
                         LIMIT @Tamano OFFSET @Offset;";

        using var cn = new NpgsqlConnection(_cs);
        var total = await cn.ExecuteScalarAsync<int>(sqlCount, parametros);
        var items = (await cn.QueryAsync<GarantiaDTO>(sqlData, parametros)).ToList();

        return new ResultadoPaginado<GarantiaDTO>
        {
            Items = items,
            TotalRegistros = total,
            PaginaActual = pagina,
            TamanoPagina = tamano
        };
    }

    public async Task<GarantiaDTO?> ObtenerPorIdAsync(int id)
    {
        var (filtroInst, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "g");

        // El expediente del bien muestra TODO el historial, incluidas anuladas
        var sql = $"{BaseSelect} WHERE g.id = @Id {filtroInst};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<GarantiaDTO>(sql,
            new { Id = id, InstitucionId = _sesion.InstitucionId });
    }

    public async Task<IEnumerable<GarantiaDTO>> ObtenerPorBienAsync(int bienId)
    {
        var (filtroInst, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "g");

        // El expediente del bien muestra TODO el historial, incluidas anuladas
        var sql = $@"{BaseSelect}
                     WHERE g.bien_id = @BienId {filtroInst}
                     ORDER BY g.fecha_vencimiento DESC;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<GarantiaDTO>(sql,
            new { BienId = bienId, InstitucionId = _sesion.InstitucionId });
    }

    public async Task<int> CrearAsync(GarantiaDTO dto)
    {
        const string sql = @"
            INSERT INTO garantias
                (bien_id, institucion_id, proveedor_id, numero_garantia, tipo,
                 fecha_inicio, fecha_vencimiento, condiciones, contacto_proveedor,
                 observaciones, anulada, created_at, updated_at)
            VALUES
                (@BienId, @InstitucionId, @ProveedorId, @NumeroGarantia, @Tipo,
                 @FechaInicio, @FechaVencimiento, @Condiciones, @ContactoProveedor,
                 @Observaciones, FALSE, NOW(), NOW())
            RETURNING id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, new
        {
            dto.BienId,
            InstitucionId = _sesion.InstitucionId,
            dto.ProveedorId,
            dto.NumeroGarantia,
            dto.Tipo,
            FechaInicio = dto.FechaInicio.Date,
            FechaVencimiento = dto.FechaVencimiento.Date,
            dto.Condiciones,
            dto.ContactoProveedor,
            dto.Observaciones
        });
    }

    public async Task ActualizarAsync(GarantiaDTO dto)
    {
        const string sql = @"
            UPDATE garantias
            SET proveedor_id = @ProveedorId,
                numero_garantia = @NumeroGarantia,
                tipo = @Tipo,
                fecha_inicio = @FechaInicio,
                fecha_vencimiento = @FechaVencimiento,
                condiciones = @Condiciones,
                contacto_proveedor = @ContactoProveedor,
                observaciones = @Observaciones,
                updated_at = NOW()
            WHERE id = @Id AND anulada = FALSE;";

        using var cn = new NpgsqlConnection(_cs);
        await cn.ExecuteAsync(sql, new
        {
            dto.Id,
            dto.ProveedorId,
            dto.NumeroGarantia,
            dto.Tipo,
            FechaInicio = dto.FechaInicio.Date,
            FechaVencimiento = dto.FechaVencimiento.Date,
            dto.Condiciones,
            dto.ContactoProveedor,
            dto.Observaciones
        });
    }

    /// <summary>
    /// Anula lógicamente una garantía (soft-delete).
    /// REGLA DE NEGOCIO: solo se pueden anular garantías VENCIDAS.
    /// El registro se preserva para auditoría.
    /// </summary>
    public async Task AnularAsync(int id, string motivo)
    {
        const string sql = @"
            UPDATE garantias
            SET anulada = TRUE,
                anulada_por = @UsuarioId,
                anulada_fecha = NOW(),
                anulada_motivo = @Motivo,
                updated_at = NOW()
            WHERE id = @Id 
              AND anulada = FALSE
              AND fecha_vencimiento < NOW()::date;";

        using var cn = new NpgsqlConnection(_cs);
        await cn.ExecuteAsync(sql, new
        {
            Id = id,
            UsuarioId = _sesion.UsuarioActual?.Id,
            Motivo = motivo
        });
    }

    public async Task<(int Vigentes, int PorVencer, int Vencidas, int Total)> ObtenerMetricasAsync()
    {
        var (filtroInst, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "g");

        var sql = $@"
            SELECT
                COUNT(*) FILTER (WHERE g.fecha_vencimiento > NOW()::date + INTERVAL '30 days')::int AS Vigentes,
                COUNT(*) FILTER (WHERE g.fecha_vencimiento >= NOW()::date 
                                 AND g.fecha_vencimiento <= NOW()::date + INTERVAL '30 days')::int AS PorVencer,
                COUNT(*) FILTER (WHERE g.fecha_vencimiento < NOW()::date)::int AS Vencidas,
                COUNT(*)::int AS Total
            FROM garantias g
            WHERE g.anulada = FALSE {filtroInst};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstAsync<(int Vigentes, int PorVencer, int Vencidas, int Total)>(
            sql, new { InstitucionId = _sesion.InstitucionId });
    }
}