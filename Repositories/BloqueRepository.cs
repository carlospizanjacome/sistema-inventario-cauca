using Almacen.DTOs;
using Almacen.Helpers;
using Almacen.Interfaces;
using Almacen.Services;
using Dapper;
using Npgsql;

namespace Almacen.Repositories;

public class BloqueRepository : IBloqueRepository
{
    private readonly string _cs;
    private readonly UsuarioSesionService _sesion;

    public BloqueRepository(IConfiguration cfg, UsuarioSesionService sesion)
    {
        _cs = cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta DefaultConnection.");
        _sesion = sesion;
    }

    private const string BaseSelect = @"
        SELECT b.id, b.sede_id AS SedeId, b.nombre, b.descripcion, b.pisos,
               b.activo, b.created_at AS CreatedAt,
               s.nombre AS SedeNombre,
               i.nombre AS InstitucionNombre
        FROM bloques b
        LEFT JOIN sedes s         ON s.id = b.sede_id
        LEFT JOIN instituciones i ON i.id = s.institucion_id";

    private bool DebeFiltrarPorSede()
        => !_sesion.VeTodasLasSedes;

    private (string Sql, DynamicParameters Parametros) ConstruirFiltros()
    {
        var condiciones = new List<string>();
        var parametros = new DynamicParameters();

        if (!_sesion.EsSuperAdmin && _sesion.InstitucionId > 0)
        {
            condiciones.Add("s.institucion_id = @InstitucionId");
            parametros.Add("InstitucionId", _sesion.InstitucionId);
        }

        if (DebeFiltrarPorSede())
        {
            condiciones.Add("b.sede_id = @SedeId");
            parametros.Add("SedeId", _sesion.SedeId!.Value);
        }

        var sql = condiciones.Count > 0
            ? " AND " + string.Join(" AND ", condiciones)
            : string.Empty;

        return (sql, parametros);
    }

    private void AsignarSedeObligatoria(BloqueDTO dto)
    {
        if (!_sesion.VeTodasLasSedes)
        {
            dto.SedeId = _sesion.SedeId ?? 0;
        }

        if (dto.SedeId <= 0)
        {
            throw new InvalidOperationException(
                "La sede es obligatoria. Seleccione una sede.");
        }
    }

    public async Task<IEnumerable<BloqueDTO>> ObtenerTodosAsync()
    {
        var (filtro, parametros) = ConstruirFiltros();
        var sql = $"{BaseSelect} WHERE 1=1 {filtro} ORDER BY i.nombre, s.nombre, b.nombre;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<BloqueDTO>(sql, parametros);
    }

    public async Task<ResultadoPaginado<BloqueDTO>> ObtenerPaginadoAsync(
        int pagina = 1,
        int tamano = 25,
        string? filtroTexto = null,
        int? sedeId = null)
    {
        if (pagina < 1) pagina = 1;
        if (tamano < 1) tamano = 25;
        if (tamano > 200) tamano = 200;

        var (filtroBase, parametros) = ConstruirFiltros();

        var condiciones = new List<string>();

        if (!string.IsNullOrWhiteSpace(filtroTexto))
        {
            condiciones.Add("(b.nombre ILIKE @Buscar OR b.descripcion ILIKE @Buscar)");
            parametros.Add("Buscar", $"%{filtroTexto}%");
        }

        if (sedeId.HasValue && sedeId.Value > 0)
        {
            condiciones.Add("b.sede_id = @FiltroSedeId");
            parametros.Add("FiltroSedeId", sedeId.Value);
        }

        var whereExtra = condiciones.Count > 0
            ? " AND " + string.Join(" AND ", condiciones)
            : string.Empty;

        parametros.Add("Tamano", tamano);
        parametros.Add("Offset", (pagina - 1) * tamano);

        var sqlCount = $@"
            SELECT COUNT(*)
            FROM bloques b
            LEFT JOIN sedes s ON s.id = b.sede_id
            WHERE 1=1 {filtroBase} {whereExtra};";

        var sqlData = $@"
            {BaseSelect}
            WHERE 1=1 {filtroBase} {whereExtra}
            ORDER BY i.nombre, s.nombre, b.nombre
            LIMIT @Tamano OFFSET @Offset;";

        using var cn = new NpgsqlConnection(_cs);

        var total = await cn.ExecuteScalarAsync<int>(sqlCount, parametros);
        var items = (await cn.QueryAsync<BloqueDTO>(sqlData, parametros)).ToList();

        return new ResultadoPaginado<BloqueDTO>
        {
            Items = items,
            TotalRegistros = total,
            PaginaActual = pagina,
            TamanoPagina = tamano
        };
    }

    public async Task<IEnumerable<BloqueDTO>> ObtenerPorSedeAsync(int sedeId)
    {
        var (filtro, parametros) = ConstruirFiltros();
        parametros.Add("SedeParam", sedeId);

        var sql = $"{BaseSelect} WHERE b.sede_id = @SedeParam {filtro} ORDER BY b.nombre;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<BloqueDTO>(sql, parametros);
    }

    public async Task<BloqueDTO?> ObtenerPorIdAsync(int id)
    {
        var (filtro, parametros) = ConstruirFiltros();
        parametros.Add("Id", id);

        var sql = $"{BaseSelect} WHERE b.id = @Id {filtro};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<BloqueDTO>(sql, parametros);
    }

    public async Task<int> CrearAsync(BloqueDTO dto)
    {
        AsignarSedeObligatoria(dto);

        const string sql = @"
            INSERT INTO bloques (sede_id, nombre, descripcion, pisos, activo)
            VALUES (@SedeId, @Nombre, @Descripcion, @Pisos, @Activo)
            RETURNING id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, dto);
    }

    public async Task<bool> ActualizarAsync(BloqueDTO dto)
    {
        AsignarSedeObligatoria(dto);

        const string sql = @"
            UPDATE bloques
            SET sede_id = @SedeId, nombre = @Nombre, descripcion = @Descripcion,
                pisos = @Pisos, activo = @Activo
            WHERE id = @Id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync(sql, dto) > 0;
    }

    public async Task<bool> EliminarAsync(int id)
    {
        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync("DELETE FROM bloques WHERE id = @Id;",
            new { Id = id }) > 0;
    }

    public async Task<bool> TieneAulasAsync(int bloqueId)
    {
        using var cn = new NpgsqlConnection(_cs);
        var n = await cn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM aulas WHERE bloque_id = @Id;", new { Id = bloqueId });
        return n > 0;
    }
}