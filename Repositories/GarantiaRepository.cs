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
            g.id,
            g.bien_id                 AS BienId,
            g.institucion_id          AS InstitucionId,
            g.sede_id                 AS SedeId,
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
            p.nit                     AS ProveedorNit,
            sed.nombre                AS SedeNombre
        FROM garantias g
        LEFT JOIN bienes b       ON b.id = g.bien_id
        LEFT JOIN categorias c   ON c.id = b.categoria_id
        LEFT JOIN proveedores p  ON p.id = g.proveedor_id
        LEFT JOIN sedes sed      ON sed.id = g.sede_id";

    public async Task<ResultadoPaginado<GarantiaDTO>> ObtenerPaginadoAsync(
        int pagina, int tamano,
        string? filtroTexto,
        string? filtroEstado,
        string? filtroTipo)
    {
        if (pagina < 1) pagina = 1;
        if (tamano < 1) tamano = 25;
        if (tamano > 200) tamano = 200;

        var (filtroSql, parametros) = ConstruirFiltros("g");

        var condiciones = new List<string> { "g.anulada = FALSE" };

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

        var whereExtra = " AND " + string.Join(" AND ", condiciones);

        parametros.Add("Tamano", tamano);
        parametros.Add("Offset", (pagina - 1) * tamano);

        var sqlCount = $@"SELECT COUNT(*) FROM garantias g
                          LEFT JOIN bienes b ON b.id = g.bien_id
                          WHERE 1=1 {filtroSql} {whereExtra};";

        var sqlData = $@"{BaseSelect}
                         WHERE 1=1 {filtroSql} {whereExtra}
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
        var (filtroSql, parametros) = ConstruirFiltros("g");
        parametros.Add("Id", id);

        var sql = $"{BaseSelect} WHERE g.id = @Id {filtroSql};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<GarantiaDTO>(sql, parametros);
    }

    public async Task<IEnumerable<GarantiaDTO>> ObtenerPorBienAsync(int bienId)
    {
        var (filtroSql, parametros) = ConstruirFiltros("g");
        parametros.Add("BienId", bienId);

        var sql = $@"{BaseSelect}
                     WHERE g.bien_id = @BienId {filtroSql}
                     ORDER BY g.fecha_vencimiento DESC;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<GarantiaDTO>(sql, parametros);
    }

    public async Task<int> CrearAsync(GarantiaDTO dto)
    {
        if (!_sesion.EsSuperAdmin)
            dto.InstitucionId = _sesion.InstitucionId;

        if (!_sesion.VeTodasLasSedes)
            dto.SedeId = _sesion.SedeId;

        if (dto.SedeId == null)
            throw new InvalidOperationException("La sede es obligatoria para registrar la garantía.");

        const string sql = @"
            INSERT INTO garantias
                (bien_id, institucion_id, sede_id, proveedor_id, numero_garantia, tipo,
                 fecha_inicio, fecha_vencimiento, condiciones, contacto_proveedor,
                 observaciones, anulada, created_at, updated_at)
            VALUES
                (@BienId, @InstitucionId, @SedeId, @ProveedorId, @NumeroGarantia, @Tipo,
                 @FechaInicio, @FechaVencimiento, @Condiciones, @ContactoProveedor,
                 @Observaciones, FALSE, NOW(), NOW())
            RETURNING id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, new
        {
            dto.BienId,
            dto.InstitucionId,
            dto.SedeId,
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
        var (filtroSql, parametros) = ConstruirFiltros("g");

        var sql = $@"
            SELECT
                COUNT(*) FILTER (WHERE g.fecha_vencimiento > NOW()::date + INTERVAL '30 days')::int AS Vigentes,
                COUNT(*) FILTER (WHERE g.fecha_vencimiento >= NOW()::date 
                                 AND g.fecha_vencimiento <= NOW()::date + INTERVAL '30 days')::int AS PorVencer,
                COUNT(*) FILTER (WHERE g.fecha_vencimiento < NOW()::date)::int AS Vencidas,
                COUNT(*)::int AS Total
            FROM garantias g
            WHERE g.anulada = FALSE {filtroSql};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstAsync<(int Vigentes, int PorVencer, int Vencidas, int Total)>(sql, parametros);
    }
}