using Almacen.DTOs;
using Almacen.Helpers;
using Almacen.Interfaces;
using Almacen.Services;
using Dapper;
using Npgsql;

namespace Almacen.Repositories;

public class SedeRepository : ISedeRepository
{
    private readonly string _cs;
    private readonly UsuarioSesionService _sesion;

    public SedeRepository(IConfiguration cfg, UsuarioSesionService sesion)
    {
        _cs = cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta DefaultConnection.");
        _sesion = sesion;
    }

    // ═══════════════════════════════════════════════════════════
    // ✅ FIX CRÍTICO: agregadas s.latitud y s.longitud al SELECT
    // Sin esto el mapa nunca recibe coordenadas y no pinta pines.
    // ═══════════════════════════════════════════════════════════
    private const string BaseSelect = @"
        SELECT s.id, s.institucion_id AS InstitucionId, s.nombre, s.direccion, s.telefono,
               s.tipo, s.activo,
               s.latitud, s.longitud,
               s.created_at AS CreatedAt,
               i.nombre AS InstitucionNombre
        FROM sedes s
        LEFT JOIN instituciones i ON i.id = s.institucion_id";

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
            condiciones.Add($"{alias}.id = @SedeId");
            parametros.Add("SedeId", _sesion.SedeId!.Value);
        }

        var sql = condiciones.Count > 0
            ? " AND " + string.Join(" AND ", condiciones)
            : string.Empty;

        return (sql, parametros);
    }

    public async Task<IEnumerable<SedeDTO>> ObtenerTodasAsync()
    {
        var (filtro, parametros) = ConstruirFiltros("s");
        var sql = $"{BaseSelect} WHERE 1=1 {filtro} ORDER BY i.nombre, s.nombre;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<SedeDTO>(sql, parametros);
    }

    public async Task<SedeDTO?> ObtenerPorIdAsync(int id)
    {
        var (filtro, parametros) = ConstruirFiltros("s");
        parametros.Add("Id", id);

        var sql = $"{BaseSelect} WHERE s.id = @Id {filtro};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<SedeDTO>(sql, parametros);
    }

    // ✅ FIX: ahora guarda latitud y longitud al crear
    public async Task<int> CrearAsync(SedeDTO dto)
    {
        if (!_sesion.VeTodasLasSedes)
        {
            throw new InvalidOperationException(
                "No tiene permisos para crear sedes.");
        }

        if (!_sesion.EsSuperAdmin)
        {
            dto.InstitucionId = _sesion.InstitucionId;
        }

        const string sql = @"
            INSERT INTO sedes (institucion_id, nombre, direccion, telefono, tipo, activo, latitud, longitud)
            VALUES (@InstitucionId, @Nombre, @Direccion, @Telefono, @Tipo, @Activo, @Latitud, @Longitud)
            RETURNING id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, dto);
    }

    // ✅ FIX: ahora actualiza latitud y longitud al editar
    public async Task<bool> ActualizarAsync(SedeDTO dto)
    {
        if (!_sesion.VeTodasLasSedes)
        {
            throw new InvalidOperationException(
                "No tiene permisos para editar sedes.");
        }

        if (!_sesion.EsSuperAdmin)
        {
            dto.InstitucionId = _sesion.InstitucionId;
        }

        const string sql = @"
            UPDATE sedes
            SET institucion_id = @InstitucionId,
                nombre         = @Nombre,
                direccion      = @Direccion,
                telefono       = @Telefono,
                tipo           = @Tipo,
                activo         = @Activo,
                latitud        = @Latitud,
                longitud       = @Longitud
            WHERE id = @Id
              AND (@EsSuperAdmin = TRUE OR institucion_id = @InstitucionId);";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync(sql, new
        {
            dto.Id,
            dto.InstitucionId,
            dto.Nombre,
            dto.Direccion,
            dto.Telefono,
            dto.Tipo,
            dto.Activo,
            dto.Latitud,
            dto.Longitud,
            EsSuperAdmin = _sesion.EsSuperAdmin
        }) > 0;
    }

    // ✅ NUEVO: para georreferenciar arrastrando un pin desde el mapa
    public async Task<bool> ActualizarCoordenadasAsync(int sedeId, decimal latitud, decimal longitud)
    {
        if (!_sesion.VeTodasLasSedes)
        {
            throw new InvalidOperationException(
                "No tiene permisos para georreferenciar sedes.");
        }

        const string sql = @"
            UPDATE sedes
            SET latitud  = @Latitud,
                longitud = @Longitud
            WHERE id = @Id
              AND (@EsSuperAdmin = TRUE OR institucion_id = @InstitucionId);";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync(sql, new
        {
            Id = sedeId,
            Latitud = latitud,
            Longitud = longitud,
            InstitucionId = _sesion.InstitucionId,
            EsSuperAdmin = _sesion.EsSuperAdmin
        }) > 0;
    }

    public async Task<bool> EliminarAsync(int id)
    {
        if (!_sesion.VeTodasLasSedes)
        {
            throw new InvalidOperationException(
                "No tiene permisos para eliminar sedes.");
        }

        const string sql = @"
            DELETE FROM sedes
            WHERE id = @Id
              AND (@EsSuperAdmin = TRUE OR institucion_id = @InstitucionId);";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync(sql, new
        {
            Id = id,
            InstitucionId = _sesion.InstitucionId,
            EsSuperAdmin = _sesion.EsSuperAdmin
        }) > 0;
    }

    public async Task<bool> TieneBloquesAsync(int sedeId)
    {
        using var cn = new NpgsqlConnection(_cs);
        var n = await cn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM bloques WHERE sede_id = @Id;", new { Id = sedeId });
        return n > 0;
    }
}