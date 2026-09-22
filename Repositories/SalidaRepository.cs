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

        var sql = $"{BaseSelect} WHERE 1=1 {filtro} ORDER BY s.fecha_salida DESC, s.id DESC;";

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
                 institucion_id)
            VALUES
                (@BienId, @TipoBaja, @Motivo, @FechaSalida, @NumeroActaComite,
                 @NumeroDenuncia, @ValorSalida, @FuncionarioApruebaId, @Observaciones,
                 @InstitucionId)
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
            WHERE id = @Id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync(sql, dto) > 0;
    }

    public async Task<bool> EliminarAsync(int id)
    {
        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync("DELETE FROM salidas WHERE id = @Id;",
            new { Id = id }) > 0;
    }

    public async Task<bool> BienTieneSalidaAsync(int bienId, int? excluirId = null)
    {
        const string sql = @"
            SELECT COUNT(*) FROM salidas
            WHERE bien_id = @BienId
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
            WHERE 1=1 {filtroInst} {filtroBusqueda};";

        var sqlData = $@"
            {BaseSelect}
            WHERE 1=1 {filtroInst} {filtroBusqueda}
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

        var condiciones = new List<string>();
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

        var whereExtra = condiciones.Any() ? " AND " + string.Join(" AND ", condiciones) : "";

        parametros.Add("Tamano", tamano);
        parametros.Add("Offset", (pagina - 1) * tamano);

        var sqlCount = $@"
            SELECT COUNT(*) FROM salidas s
            LEFT JOIN bienes b ON b.id = s.bien_id
            WHERE 1=1 {filtroInst} {whereExtra};";

        var sqlData = $@"
            {BaseSelect}
            WHERE 1=1 {filtroInst} {whereExtra}
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