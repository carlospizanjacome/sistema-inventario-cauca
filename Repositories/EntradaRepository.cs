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
            b.codigo               AS BienCodigo,
            b.nombre               AS BienNombre,
            f.nombre_completo      AS FuncionarioNombre,
            p.nombre               AS ProveedorNombre
        FROM entradas e
        LEFT JOIN bienes b       ON b.id = e.bien_id
        LEFT JOIN funcionarios f ON f.id = e.funcionario_recibe_id
        LEFT JOIN proveedores p  ON p.id = e.proveedor_id";

    public async Task<IEnumerable<EntradaDTO>> ObtenerTodasAsync()
    {
        var (filtro, param) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "e");

        var sql = $"{BaseSelect} WHERE 1=1 {filtro} ORDER BY e.fecha_entrada DESC, e.id DESC;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<EntradaDTO>(sql, param);
    }

    public async Task<EntradaDTO?> ObtenerPorIdAsync(int id)
    {
        var (filtro, param) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "e");

        var sql = $"{BaseSelect} WHERE e.id = @Id {filtro};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<EntradaDTO>(sql,
            new { Id = id, InstitucionId = _sesion.InstitucionId });
    }

    public async Task<int> CrearAsync(EntradaDTO dto)
    {
        const string sql = @"
            INSERT INTO entradas
                (bien_id, tipo_fuente, numero_factura, fecha_entrada, valor,
                 proveedor, proveedor_id, funcionario_recibe_id, observaciones,
                 institucion_id)
            VALUES
                (@BienId, @TipoFuente, @NumeroFactura, @FechaEntrada, @Valor,
                 @Proveedor, @ProveedorId, @FuncionarioRecibeId, @Observaciones,
                 @InstitucionId)
            RETURNING id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, new
        {
            dto.BienId,
            dto.TipoFuente,
            dto.NumeroFactura,
            dto.FechaEntrada,
            dto.Valor,
            dto.Proveedor,
            dto.ProveedorId,
            dto.FuncionarioRecibeId,
            dto.Observaciones,
            InstitucionId = _sesion.InstitucionId
        });
    }

    public async Task<bool> ActualizarAsync(EntradaDTO dto)
    {
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
                updated_at = NOW()
            WHERE id = @Id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync(sql, dto) > 0;
    }

    public async Task<bool> EliminarAsync(int id)
    {
        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync("DELETE FROM entradas WHERE id = @Id;",
            new { Id = id }) > 0;
    }

    public async Task<bool> BienTieneEntradaAsync(int bienId, int? excluirId = null)
    {
        const string sql = @"
            SELECT COUNT(*) FROM entradas
            WHERE bien_id = @BienId
              AND (@ExcluirId IS NULL OR id <> @ExcluirId);";

        using var cn = new NpgsqlConnection(_cs);
        var n = await cn.ExecuteScalarAsync<int>(sql,
            new { BienId = bienId, ExcluirId = excluirId });
        return n > 0;
    }

    public async Task<ResultadoPaginado<EntradaDTO>> ObtenerPaginadoAsync(
        int pagina = 1,
        int tamano = 25,
        string? filtroTexto = null)
    {
        if (pagina < 1) pagina = 1;
        if (tamano < 1) tamano = 25;
        if (tamano > 200) tamano = 200;

        var (filtroInst, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "e");

        var filtroBusqueda = string.Empty;
        if (!string.IsNullOrWhiteSpace(filtroTexto))
        {
            filtroBusqueda = @" AND (b.codigo ILIKE @Buscar 
                                 OR b.nombre ILIKE @Buscar 
                                 OR e.numero_factura ILIKE @Buscar 
                                 OR e.proveedor ILIKE @Buscar)";
        }

        var sqlCount = $@"
            SELECT COUNT(*) FROM entradas e
            LEFT JOIN bienes b ON b.id = e.bien_id
            WHERE 1=1 {filtroInst} {filtroBusqueda};";

        var sqlData = $@"
            {BaseSelect}
            WHERE 1=1 {filtroInst} {filtroBusqueda}
            ORDER BY e.fecha_entrada DESC, e.id DESC
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
        FiltroMovimientoDTO filtro,
        int pagina = 1,
        int tamano = 25)
    {
        if (pagina < 1) pagina = 1;
        if (tamano < 1) tamano = 25;
        if (tamano > 200) tamano = 200;

        var (filtroInst, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "e");

        var condiciones = new List<string>();
        var parametros = new DynamicParameters();
        parametros.Add("InstitucionId", _sesion.InstitucionId);

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            condiciones.Add(@"(b.codigo ILIKE @Texto OR b.nombre ILIKE @Texto 
                              OR e.numero_factura ILIKE @Texto OR e.proveedor ILIKE @Texto)");
            parametros.Add("Texto", $"%{filtro.Texto}%");
        }

        if (filtro.FechaDesde.HasValue)
        {
            condiciones.Add("e.fecha_entrada >= @FechaDesde");
            parametros.Add("FechaDesde", filtro.FechaDesde.Value);
        }

        if (filtro.FechaHasta.HasValue)
        {
            condiciones.Add("e.fecha_entrada <= @FechaHasta");
            parametros.Add("FechaHasta", filtro.FechaHasta.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Tipo))
        {
            condiciones.Add("e.tipo_fuente = @Tipo");
            parametros.Add("Tipo", filtro.Tipo);
        }

        var whereExtra = condiciones.Any() ? " AND " + string.Join(" AND ", condiciones) : "";

        parametros.Add("Tamano", tamano);
        parametros.Add("Offset", (pagina - 1) * tamano);

        var sqlCount = $@"
            SELECT COUNT(*) FROM entradas e
            LEFT JOIN bienes b ON b.id = e.bien_id
            WHERE 1=1 {filtroInst} {whereExtra};";

        var sqlData = $@"
            {BaseSelect}
            WHERE 1=1 {filtroInst} {whereExtra}
            ORDER BY e.fecha_entrada DESC, e.id DESC
            LIMIT @Tamano OFFSET @Offset;";

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