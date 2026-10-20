using Almacen.DTOs;
using Almacen.Helpers;
using Almacen.Interfaces;
using Almacen.Services;
using Dapper;
using Npgsql;

namespace Almacen.Repositories;

public class MantenimientoRepository : IMantenimientoRepository
{
    private readonly string _cs;
    private readonly UsuarioSesionService _sesion;

    public MantenimientoRepository(IConfiguration cfg, UsuarioSesionService sesion)
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
            m.id,
            m.bien_id                    AS BienId,
            m.institucion_id             AS InstitucionId,
            m.sede_id                    AS SedeId,
            m.tipo,
            m.estado,
            m.fecha_ingreso::timestamp            AS FechaIngreso,
            m.fecha_salida::timestamp             AS FechaSalida,
            m.fecha_devolucion_prevista::timestamp AS FechaDevolucionPrevista,
            m.proveedor_id               AS ProveedorId,
            m.tecnico_responsable        AS TecnicoResponsable,
            m.diagnostico,
            m.trabajo_realizado          AS TrabajoRealizado,
            m.repuestos_utilizados       AS RepuestosUtilizados,
            m.costo,
            m.estado_bien_ingreso        AS EstadoBienIngreso,
            m.estado_bien_egreso         AS EstadoBienEgreso,
            m.observaciones,
            m.anulada,
            m.anulada_por                AS AnuladaPor,
            m.anulada_fecha::timestamp   AS AnuladaFecha,
            m.anulada_motivo             AS AnuladaMotivo,
            m.created_at                 AS CreatedAt,
            m.updated_at                 AS UpdatedAt,
            b.codigo                     AS BienCodigo,
            b.nombre                     AS BienNombre,
            c.nombre                     AS BienCategoriaNombre,
            p.nombre                     AS ProveedorNombre,
            p.nit                        AS ProveedorNit,
            sed.nombre                   AS SedeNombre
        FROM mantenimientos m
        LEFT JOIN bienes b       ON b.id = m.bien_id
        LEFT JOIN categorias c   ON c.id = b.categoria_id
        LEFT JOIN proveedores p  ON p.id = m.proveedor_id
        LEFT JOIN sedes sed      ON sed.id = m.sede_id";

    public async Task<ResultadoPaginado<MantenimientoDTO>> ObtenerPaginadoAsync(
        int pagina, int tamano,
        string? filtroTexto,
        string? filtroEstado,
        string? filtroTipo,
        DateTime? filtroFechaDesde,
        DateTime? filtroFechaHasta)
    {
        if (pagina < 1) pagina = 1;
        if (tamano < 1) tamano = 25;
        if (tamano > 200) tamano = 200;

        var (filtroSql, parametros) = ConstruirFiltros("m");

        var condiciones = new List<string> { "m.anulada = FALSE" };

        if (!string.IsNullOrWhiteSpace(filtroTexto))
        {
            condiciones.Add(@"(b.codigo ILIKE @Buscar 
                             OR b.nombre ILIKE @Buscar 
                             OR m.tecnico_responsable ILIKE @Buscar 
                             OR m.diagnostico ILIKE @Buscar)");
            parametros.Add("Buscar", $"%{filtroTexto}%");
        }

        if (!string.IsNullOrWhiteSpace(filtroEstado))
        {
            condiciones.Add("m.estado = @Estado");
            parametros.Add("Estado", filtroEstado);
        }

        if (!string.IsNullOrWhiteSpace(filtroTipo))
        {
            condiciones.Add("m.tipo = @Tipo");
            parametros.Add("Tipo", filtroTipo);
        }

        if (filtroFechaDesde.HasValue)
        {
            condiciones.Add("m.fecha_ingreso >= @FechaDesde");
            parametros.Add("FechaDesde", filtroFechaDesde.Value.Date);
        }

        if (filtroFechaHasta.HasValue)
        {
            condiciones.Add("m.fecha_ingreso <= @FechaHasta");
            parametros.Add("FechaHasta", filtroFechaHasta.Value.Date);
        }

        var whereExtra = " AND " + string.Join(" AND ", condiciones);

        parametros.Add("Tamano", tamano);
        parametros.Add("Offset", (pagina - 1) * tamano);

        var sqlCount = $@"SELECT COUNT(*) FROM mantenimientos m
                          LEFT JOIN bienes b ON b.id = m.bien_id
                          WHERE 1=1 {filtroSql} {whereExtra};";

        var sqlData = $@"{BaseSelect}
                         WHERE 1=1 {filtroSql} {whereExtra}
                         ORDER BY 
                            CASE m.estado
                                WHEN 'EN_PROCESO' THEN 1
                                WHEN 'ABIERTO' THEN 2
                                WHEN 'CERRADO' THEN 3
                                WHEN 'ANULADO' THEN 4
                                ELSE 5
                            END,
                            m.fecha_ingreso DESC,
                            m.id DESC
                         LIMIT @Tamano OFFSET @Offset;";

        using var cn = new NpgsqlConnection(_cs);
        var total = await cn.ExecuteScalarAsync<int>(sqlCount, parametros);
        var items = (await cn.QueryAsync<MantenimientoDTO>(sqlData, parametros)).ToList();

        return new ResultadoPaginado<MantenimientoDTO>
        {
            Items = items,
            TotalRegistros = total,
            PaginaActual = pagina,
            TamanoPagina = tamano
        };
    }

    public async Task<MantenimientoDTO?> ObtenerPorIdAsync(int id)
    {
        var (filtroSql, parametros) = ConstruirFiltros("m");
        parametros.Add("Id", id);

        var sql = $"{BaseSelect} WHERE m.id = @Id {filtroSql};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<MantenimientoDTO>(sql, parametros);
    }

    public async Task<IEnumerable<MantenimientoDTO>> ObtenerPorBienAsync(int bienId)
    {
        var (filtroSql, parametros) = ConstruirFiltros("m");
        parametros.Add("BienId", bienId);

        var sql = $@"{BaseSelect}
                     WHERE m.bien_id = @BienId {filtroSql}
                     ORDER BY m.fecha_ingreso DESC, m.id DESC;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<MantenimientoDTO>(sql, parametros);
    }

    public async Task<int> CrearAsync(MantenimientoDTO dto)
    {
        if (!_sesion.EsSuperAdmin)
            dto.InstitucionId = _sesion.InstitucionId;

        if (!_sesion.VeTodasLasSedes)
            dto.SedeId = _sesion.SedeId;

        if (dto.SedeId == null)
            throw new InvalidOperationException("La sede es obligatoria para registrar el mantenimiento.");

        const string sql = @"
            INSERT INTO mantenimientos
                (bien_id, institucion_id, sede_id, tipo, estado,
                 fecha_ingreso, fecha_devolucion_prevista,
                 proveedor_id, tecnico_responsable,
                 diagnostico, costo, estado_bien_ingreso,
                 observaciones, anulada,
                 created_at, updated_at)
            VALUES
                (@BienId, @InstitucionId, @SedeId, @Tipo, 'ABIERTO',
                 @FechaIngreso, @FechaDevolucionPrevista,
                 @ProveedorId, @TecnicoResponsable,
                 @Diagnostico, @Costo, @EstadoBienIngreso,
                 @Observaciones, FALSE,
                 NOW(), NOW())
            RETURNING id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, new
        {
            dto.BienId,
            dto.InstitucionId,
            dto.SedeId,
            dto.Tipo,
            FechaIngreso = dto.FechaIngreso.Date,
            FechaDevolucionPrevista = dto.FechaDevolucionPrevista?.Date,
            dto.ProveedorId,
            dto.TecnicoResponsable,
            dto.Diagnostico,
            dto.Costo,
            dto.EstadoBienIngreso,
            dto.Observaciones
        });
    }

    public async Task ActualizarAsync(MantenimientoDTO dto)
    {
        const string sql = @"
            UPDATE mantenimientos
            SET tipo = @Tipo,
                fecha_ingreso = @FechaIngreso,
                fecha_devolucion_prevista = @FechaDevolucionPrevista,
                proveedor_id = @ProveedorId,
                tecnico_responsable = @TecnicoResponsable,
                diagnostico = @Diagnostico,
                costo = @Costo,
                estado_bien_ingreso = @EstadoBienIngreso,
                observaciones = @Observaciones,
                updated_at = NOW()
            WHERE id = @Id AND estado IN ('ABIERTO','EN_PROCESO') AND anulada = FALSE;";

        using var cn = new NpgsqlConnection(_cs);
        await cn.ExecuteAsync(sql, new
        {
            dto.Id,
            dto.Tipo,
            FechaIngreso = dto.FechaIngreso.Date,
            FechaDevolucionPrevista = dto.FechaDevolucionPrevista?.Date,
            dto.ProveedorId,
            dto.TecnicoResponsable,
            dto.Diagnostico,
            dto.Costo,
            dto.EstadoBienIngreso,
            dto.Observaciones
        });
    }

    public async Task CerrarAsync(int id, DateTime fechaSalida, string trabajoRealizado,
                                  string? repuestos, decimal costo, string? estadoBienEgreso,
                                  string? observaciones)
    {
        const string sql = @"
            UPDATE mantenimientos
            SET estado = 'CERRADO',
                fecha_salida = @FechaSalida,
                trabajo_realizado = @TrabajoRealizado,
                repuestos_utilizados = @Repuestos,
                costo = @Costo,
                estado_bien_egreso = @EstadoBienEgreso,
                observaciones = COALESCE(@Observaciones, observaciones),
                updated_at = NOW()
            WHERE id = @Id AND estado IN ('ABIERTO','EN_PROCESO') AND anulada = FALSE;";

        using var cn = new NpgsqlConnection(_cs);
        await cn.ExecuteAsync(sql, new
        {
            Id = id,
            FechaSalida = fechaSalida.Date,
            TrabajoRealizado = trabajoRealizado,
            Repuestos = repuestos,
            Costo = costo,
            EstadoBienEgreso = estadoBienEgreso,
            Observaciones = observaciones
        });
    }

    public async Task AnularAsync(int id, string motivo)
    {
        const string sql = @"
            UPDATE mantenimientos
            SET anulada = TRUE,
                anulada_por = @UsuarioId,
                anulada_fecha = NOW(),
                anulada_motivo = @Motivo,
                updated_at = NOW()
            WHERE id = @Id 
              AND estado IN ('CERRADO', 'ANULADO')
              AND anulada = FALSE;";

        using var cn = new NpgsqlConnection(_cs);
        await cn.ExecuteAsync(sql, new
        {
            Id = id,
            UsuarioId = _sesion.UsuarioActual?.Id,
            Motivo = motivo
        });
    }

    public async Task<(int Abiertos, int EnProceso, int CerradosMes, decimal CostoMes)> ObtenerMetricasAsync()
    {
        var (filtroSql, parametros) = ConstruirFiltros("m");

        var sql = $@"
            SELECT
                COUNT(*) FILTER (WHERE m.estado = 'ABIERTO')::int AS Abiertos,
                COUNT(*) FILTER (WHERE m.estado = 'EN_PROCESO')::int AS EnProceso,
                COUNT(*) FILTER (WHERE m.estado = 'CERRADO' 
                                 AND DATE_TRUNC('month', m.fecha_salida) = DATE_TRUNC('month', NOW()))::int AS CerradosMes,
                COALESCE(SUM(m.costo) FILTER (WHERE m.estado = 'CERRADO'
                                 AND DATE_TRUNC('month', m.fecha_salida) = DATE_TRUNC('month', NOW())), 0)::numeric AS CostoMes
            FROM mantenimientos m
            WHERE m.anulada = FALSE {filtroSql};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstAsync<(int Abiertos, int EnProceso, int CerradosMes, decimal CostoMes)>(sql, parametros);
    }
}