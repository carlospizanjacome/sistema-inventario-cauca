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
        LEFT JOIN bloques b ON b.id = a.bloque_id
        LEFT JOIN sedes s ON s.id = b.sede_id
        LEFT JOIN instituciones i ON i.id = s.institucion_id";

    public async Task<IEnumerable<AulaDTO>> ObtenerTodasAsync()
    {
        var (filtro, param) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "s");

        var sql = $"{BaseSelect} WHERE 1=1 {filtro} ORDER BY i.nombre, s.nombre, b.nombre, a.nombre;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<AulaDTO>(sql, param);
    }

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

        var (filtroInst, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "s");

        var condiciones = new List<string>();
        var parametros = new DynamicParameters();
        parametros.Add("InstitucionId", _sesion.InstitucionId);

        if (!string.IsNullOrWhiteSpace(filtroTexto))
        {
            condiciones.Add("a.nombre ILIKE @Buscar");
            parametros.Add("Buscar", $"%{filtroTexto}%");
        }

        if (bloqueId.HasValue && bloqueId.Value > 0)
        {
            condiciones.Add("a.bloque_id = @BloqueId");
            parametros.Add("BloqueId", bloqueId.Value);
        }

        if (!string.IsNullOrWhiteSpace(tipo))
        {
            condiciones.Add("a.tipo = @Tipo");
            parametros.Add("Tipo", tipo);
        }

        var whereExtra = condiciones.Any()
            ? " AND " + string.Join(" AND ", condiciones)
            : string.Empty;

        parametros.Add("Tamano", tamano);
        parametros.Add("Offset", (pagina - 1) * tamano);

        var sqlCount = $@"
            SELECT COUNT(*)
            FROM aulas a
            LEFT JOIN bloques b ON b.id = a.bloque_id
            LEFT JOIN sedes s ON s.id = b.sede_id
            WHERE 1=1 {filtroInst} {whereExtra};";

        var sqlData = $@"
            {BaseSelect}
            WHERE 1=1 {filtroInst} {whereExtra}
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

    public async Task<IEnumerable<AulaDTO>> ObtenerPorBloqueAsync(int bloqueId)
    {
        var (filtro, param) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "s");

        var sql = $"{BaseSelect} WHERE a.bloque_id = @BloqueId {filtro} ORDER BY a.nombre;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<AulaDTO>(sql,
            new { BloqueId = bloqueId, InstitucionId = _sesion.InstitucionId });
    }

    public async Task<AulaDTO?> ObtenerPorIdAsync(int id)
    {
        var (filtro, param) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "s");

        var sql = $"{BaseSelect} WHERE a.id = @Id {filtro};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<AulaDTO>(sql,
            new { Id = id, InstitucionId = _sesion.InstitucionId });
    }

    public async Task<int> CrearAsync(AulaDTO dto)
    {
        const string sql = @"
            INSERT INTO aulas (bloque_id, nombre, tipo, capacidad, activo)
            VALUES (@BloqueId, @Nombre, @Tipo, @Capacidad, @Activo)
            RETURNING id;";
        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, dto);
    }

    public async Task<bool> ActualizarAsync(AulaDTO dto)
    {
        const string sql = @"
            UPDATE aulas
            SET bloque_id = @BloqueId, nombre = @Nombre, tipo = @Tipo,
                capacidad = @Capacidad, activo = @Activo
            WHERE id = @Id;";
        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync(sql, dto) > 0;
    }

    public async Task<bool> EliminarAsync(int id)
    {
        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync("DELETE FROM aulas WHERE id = @Id;",
            new { Id = id }) > 0;
    }

    public async Task<bool> TieneBienesAsync(int aulaId)
    {
        using var cn = new NpgsqlConnection(_cs);
        var n = await cn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM bienes WHERE aula_id = @Id;", new { Id = aulaId });
        return n > 0;
    }
}