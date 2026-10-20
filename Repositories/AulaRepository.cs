using Almacen.DTOs;
using Almacen.Helpers;
using Almacen.Interfaces;
using Almacen.Services;
using Dapper;
using Npgsql;

namespace Almacen.Repositories;

public class AulaRepository : IAulaRepository
{
    private readonly string _cs;
    private readonly UsuarioSesionService _sesion;

    public AulaRepository(IConfiguration cfg, UsuarioSesionService sesion)
    {
        _cs = cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta DefaultConnection.");
        _sesion = sesion;
    }

    private const string BaseSelect = @"
        SELECT a.id, a.bloque_id AS BloqueId, a.nombre, a.tipo, a.capacidad,
               a.activo, a.created_at AS CreatedAt,
               b.nombre AS BloqueNombre,
               s.nombre AS SedeNombre,
               i.nombre AS InstitucionNombre
        FROM aulas a
        LEFT JOIN bloques b       ON b.id = a.bloque_id
        LEFT JOIN sedes s         ON s.id = b.sede_id
        LEFT JOIN instituciones i ON i.id = s.institucion_id";

    // ═══════════════════════════════════════════════════════════
    // Regla: usuarios que NO ven todas las sedes → filtrar por la suya
    // OJO: las aulas se relacionan con sede A TRAVÉS de bloques
    // ═══════════════════════════════════════════════════════════
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

    // ═══════════════════════════════════════════════════════════
    // Validar que el bloque elegido pertenece a la sede del usuario
    // ═══════════════════════════════════════════════════════════
    private async Task ValidarBloqueEnSedeAsync(int bloqueId)
    {
        if (bloqueId <= 0)
        {
            throw new InvalidOperationException(
                "El bloque es obligatorio. Seleccione un bloque.");
        }

        // Si ve todas las sedes, no valida más (solo que el bloque exista)
        // Si NO ve todas, valida que el bloque pertenezca a su sede
        if (!DebeFiltrarPorSede())
        {
            return;
        }

        const string sql = @"
            SELECT COUNT(*) FROM bloques
            WHERE id = @Id AND sede_id = @SedeId;";

        using var cn = new NpgsqlConnection(_cs);
        var existe = await cn.ExecuteScalarAsync<int>(sql, new
        {
            Id = bloqueId,
            SedeId = _sesion.SedeId!.Value
        });

        if (existe == 0)
        {
            throw new InvalidOperationException(
                "El bloque seleccionado no pertenece a su sede.");
        }
    }

    // ═══════════════════════════════════════════════════════════
    // OBTENER TODAS
    // ═══════════════════════════════════════════════════════════
    public async Task<IEnumerable<AulaDTO>> ObtenerTodasAsync()
    {
        var (filtro, parametros) = ConstruirFiltros();
        var sql = $"{BaseSelect} WHERE 1=1 {filtro} ORDER BY i.nombre, s.nombre, b.nombre, a.nombre;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<AulaDTO>(sql, parametros);
    }

    // ═══════════════════════════════════════════════════════════
    // OBTENER PAGINADO
    // ═══════════════════════════════════════════════════════════
    public async Task<ResultadoPaginado<AulaDTO>> ObtenerPaginadoAsync(
        int pagina = 1,
        int tamano = 25,
        string? filtroTexto = null,
        int? bloqueId = null,
        string? tipo = null)
    {
        if (pagina < 1) pagina = 1;
        if (tamano < 1) tamano = 25;
        if (tamano > 200) tamano = 200;

        var (filtroBase, parametros) = ConstruirFiltros();

        var condiciones = new List<string>();

        if (!string.IsNullOrWhiteSpace(filtroTexto))
        {
            condiciones.Add("a.nombre ILIKE @Buscar");
            parametros.Add("Buscar", $"%{filtroTexto}%");
        }

        if (bloqueId.HasValue && bloqueId.Value > 0)
        {
            condiciones.Add("a.bloque_id = @BloqueFiltroId");
            parametros.Add("BloqueFiltroId", bloqueId.Value);
        }

        if (!string.IsNullOrWhiteSpace(tipo))
        {
            condiciones.Add("a.tipo = @Tipo");
            parametros.Add("Tipo", tipo);
        }

        var whereExtra = condiciones.Count > 0
            ? " AND " + string.Join(" AND ", condiciones)
            : string.Empty;

        parametros.Add("Tamano", tamano);
        parametros.Add("Offset", (pagina - 1) * tamano);

        var sqlCount = $@"
            SELECT COUNT(*)
            FROM aulas a
            LEFT JOIN bloques b       ON b.id = a.bloque_id
            LEFT JOIN sedes s         ON s.id = b.sede_id
            LEFT JOIN instituciones i ON i.id = s.institucion_id
            WHERE 1=1 {filtroBase} {whereExtra};";

        var sqlData = $@"
            {BaseSelect}
            WHERE 1=1 {filtroBase} {whereExtra}
            ORDER BY i.nombre, s.nombre, b.nombre, a.nombre
            LIMIT @Tamano OFFSET @Offset;";

        using var cn = new NpgsqlConnection(_cs);

        var total = await cn.ExecuteScalarAsync<int>(sqlCount, parametros);
        var items = (await cn.QueryAsync<AulaDTO>(sqlData, parametros)).ToList();

        return new ResultadoPaginado<AulaDTO>
        {
            Items = items,
            TotalRegistros = total,
            PaginaActual = pagina,
            TamanoPagina = tamano
        };
    }

    // ═══════════════════════════════════════════════════════════
    // OBTENER POR BLOQUE
    // ═══════════════════════════════════════════════════════════
    public async Task<IEnumerable<AulaDTO>> ObtenerPorBloqueAsync(int bloqueId)
    {
        var (filtro, parametros) = ConstruirFiltros();
        parametros.Add("BloqueParam", bloqueId);

        var sql = $"{BaseSelect} WHERE a.bloque_id = @BloqueParam {filtro} ORDER BY a.nombre;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<AulaDTO>(sql, parametros);
    }

    // ═══════════════════════════════════════════════════════════
    // OBTENER POR ID
    // ═══════════════════════════════════════════════════════════
    public async Task<AulaDTO?> ObtenerPorIdAsync(int id)
    {
        var (filtro, parametros) = ConstruirFiltros();
        parametros.Add("Id", id);

        var sql = $"{BaseSelect} WHERE a.id = @Id {filtro};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<AulaDTO>(sql, parametros);
    }

    // ═══════════════════════════════════════════════════════════
    // CREAR
    // ═══════════════════════════════════════════════════════════
    public async Task<int> CrearAsync(AulaDTO dto)
    {
        await ValidarBloqueEnSedeAsync(dto.BloqueId);

        const string sql = @"
            INSERT INTO aulas (bloque_id, nombre, tipo, capacidad, activo)
            VALUES (@BloqueId, @Nombre, @Tipo, @Capacidad, @Activo)
            RETURNING id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, dto);
    }

    // ═══════════════════════════════════════════════════════════
    // ACTUALIZAR
    // ═══════════════════════════════════════════════════════════
    public async Task<bool> ActualizarAsync(AulaDTO dto)
    {
        await ValidarBloqueEnSedeAsync(dto.BloqueId);

        const string sql = @"
            UPDATE aulas
            SET bloque_id = @BloqueId, nombre = @Nombre, tipo = @Tipo,
                capacidad = @Capacidad, activo = @Activo
            WHERE id = @Id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync(sql, dto) > 0;
    }

    // ═══════════════════════════════════════════════════════════
    // ELIMINAR
    // ═══════════════════════════════════════════════════════════
    public async Task<bool> EliminarAsync(int id)
    {
        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync("DELETE FROM aulas WHERE id = @Id;",
            new { Id = id }) > 0;
    }

    // ═══════════════════════════════════════════════════════════
    // TIENE BIENES
    // ═══════════════════════════════════════════════════════════
    public async Task<bool> TieneBienesAsync(int aulaId)
    {
        using var cn = new NpgsqlConnection(_cs);
        var n = await cn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM bienes WHERE aula_id = @Id;", new { Id = aulaId });
        return n > 0;
    }
}