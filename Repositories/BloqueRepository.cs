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
        LEFT JOIN sedes s ON s.id = b.sede_id
        LEFT JOIN instituciones i ON i.id = s.institucion_id";

    public async Task<IEnumerable<BloqueDTO>> ObtenerTodosAsync()
    {
        var (filtro, param) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "s");

        var sql = $"{BaseSelect} WHERE 1=1 {filtro} ORDER BY i.nombre, s.nombre, b.nombre;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<BloqueDTO>(sql, param);
    }

    public async Task<IEnumerable<BloqueDTO>> ObtenerPorSedeAsync(int sedeId)
    {
        var (filtro, param) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "s");

        var sql = $"{BaseSelect} WHERE b.sede_id = @SedeId {filtro} ORDER BY b.nombre;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<BloqueDTO>(sql,
            new { SedeId = sedeId, InstitucionId = _sesion.InstitucionId });
    }

    public async Task<BloqueDTO?> ObtenerPorIdAsync(int id)
    {
        var (filtro, param) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "s");

        var sql = $"{BaseSelect} WHERE b.id = @Id {filtro};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<BloqueDTO>(sql,
            new { Id = id, InstitucionId = _sesion.InstitucionId });
    }

    public async Task<int> CrearAsync(BloqueDTO dto)
    {
        const string sql = @"
            INSERT INTO bloques (sede_id, nombre, descripcion, pisos, activo)
            VALUES (@SedeId, @Nombre, @Descripcion, @Pisos, @Activo)
            RETURNING id;";
        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, dto);
    }

    public async Task<bool> ActualizarAsync(BloqueDTO dto)
    {
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