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

    private const string BaseSelect = @"
        SELECT s.id, s.institucion_id AS InstitucionId, s.nombre, s.direccion, s.telefono,
               s.tipo, s.activo, s.created_at AS CreatedAt,
               i.nombre AS InstitucionNombre
        FROM sedes s
        LEFT JOIN instituciones i ON i.id = s.institucion_id";

    public async Task<IEnumerable<SedeDTO>> ObtenerTodasAsync()
    {
        var (filtro, param) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "s");

        var sql = $"{BaseSelect} WHERE 1=1 {filtro} ORDER BY i.nombre, s.nombre;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<SedeDTO>(sql, param);
    }

    public async Task<SedeDTO?> ObtenerPorIdAsync(int id)
    {
        var (filtro, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "s");

        var sql = $"{BaseSelect} WHERE s.id = @Id {filtro};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<SedeDTO>(sql,
            new { Id = id, InstitucionId = _sesion.InstitucionId });
    }

    public async Task<int> CrearAsync(SedeDTO dto)
    {
        if (!_sesion.EsSuperAdmin)
        {
            dto.InstitucionId = _sesion.InstitucionId;
        }

        const string sql = @"
            INSERT INTO sedes (institucion_id, nombre, direccion, telefono, tipo, activo)
            VALUES (@InstitucionId, @Nombre, @Direccion, @Telefono, @Tipo, @Activo)
            RETURNING id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, dto);
    }

    public async Task<bool> ActualizarAsync(SedeDTO dto)
    {
        if (!_sesion.EsSuperAdmin)
        {
            dto.InstitucionId = _sesion.InstitucionId;
        }

        // En UPDATE no se puede usar alias "s". Validamos con la columna real.
        const string sql = @"
            UPDATE sedes
            SET institucion_id = @InstitucionId,
                nombre         = @Nombre,
                direccion      = @Direccion,
                telefono       = @Telefono,
                tipo           = @Tipo,
                activo         = @Activo
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
            EsSuperAdmin = _sesion.EsSuperAdmin
        }) > 0;
    }

    public async Task<bool> EliminarAsync(int id)
    {
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