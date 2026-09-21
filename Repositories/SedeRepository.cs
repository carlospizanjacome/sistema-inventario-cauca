using Almacen.DTOs;
using Almacen.Interfaces;
using Dapper;
using Npgsql;

namespace Almacen.Repositories;

public class SedeRepository : ISedeRepository
{
    private readonly string _cs;
    public SedeRepository(IConfiguration cfg)
        => _cs = cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta DefaultConnection.");

    public async Task<IEnumerable<SedeDTO>> ObtenerTodasAsync()
    {
        const string sql = @"
            SELECT s.id, s.institucion_id, s.nombre, s.direccion, s.telefono,
                   s.tipo, s.activo, s.created_at AS CreatedAt,
                   i.nombre AS institucion_nombre
            FROM sedes s
            LEFT JOIN instituciones i ON i.id = s.institucion_id
            ORDER BY i.nombre, s.nombre;";
        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<SedeDTO>(sql);
    }

    public async Task<SedeDTO?> ObtenerPorIdAsync(int id)
    {
        const string sql = @"
            SELECT s.id, s.institucion_id, s.nombre, s.direccion, s.telefono,
                   s.tipo, s.activo, s.created_at AS CreatedAt,
                   i.nombre AS institucion_nombre
            FROM sedes s
            LEFT JOIN instituciones i ON i.id = s.institucion_id
            WHERE s.id = @Id;";
        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<SedeDTO>(sql, new { Id = id });
    }

    public async Task<int> CrearAsync(SedeDTO dto)
    {
        const string sql = @"
            INSERT INTO sedes (institucion_id, nombre, direccion, telefono, tipo, activo)
            VALUES (@InstitucionId, @Nombre, @Direccion, @Telefono, @Tipo, @Activo)
            RETURNING id;";
        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, dto);
    }

    public async Task<bool> ActualizarAsync(SedeDTO dto)
    {
        const string sql = @"
            UPDATE sedes
            SET institucion_id = @InstitucionId, nombre = @Nombre,
                direccion = @Direccion, telefono = @Telefono,
                tipo = @Tipo, activo = @Activo
            WHERE id = @Id;";
        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync(sql, dto) > 0;
    }

    public async Task<bool> EliminarAsync(int id)
    {
        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync("DELETE FROM sedes WHERE id = @Id;", new { Id = id }) > 0;
    }

    public async Task<bool> TieneBloquesAsync(int sedeId)
    {
        using var cn = new NpgsqlConnection(_cs);
        var n = await cn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM bloques WHERE sede_id = @Id;", new { Id = sedeId });
        return n > 0;
    }
}