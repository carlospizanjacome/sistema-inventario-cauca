using Almacen.DTOs;
using Almacen.Helpers;
using Almacen.Interfaces;
using Almacen.Services;
using Dapper;
using Npgsql;

namespace Almacen.Repositories;

public class InstitucionRepository : IInstitucionRepository
{
    private readonly string _cs;
    private readonly UsuarioSesionService _sesion;

    public InstitucionRepository(IConfiguration cfg, UsuarioSesionService sesion)
    {
        _cs = cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta DefaultConnection.");
        _sesion = sesion;
    }

    /// <summary>
    /// Solo el super-admin ve TODAS las instituciones.
    /// Usuarios normales solo ven la suya.
    /// </summary>
    public async Task<IEnumerable<InstitucionDTO>> ObtenerTodasAsync()
    {
        const string sql = @"
            SELECT id, nombre, nit, codigo_dane AS CodigoDane, direccion,
                   telefono, email, rector, activo
            FROM instituciones";

        if (_sesion.EsSuperAdmin)
        {
            using var cn = new NpgsqlConnection(_cs);
            return await cn.QueryAsync<InstitucionDTO>(
                $"{sql} ORDER BY nombre;");
        }

        using var cn2 = new NpgsqlConnection(_cs);
        return await cn2.QueryAsync<InstitucionDTO>(
            $"{sql} WHERE id = @Id ORDER BY nombre;",
            new { Id = _sesion.InstitucionId });
    }

    public async Task<InstitucionDTO?> ObtenerPorIdAsync(int id)
    {
        const string sql = @"
            SELECT id, nombre, nit, codigo_dane AS CodigoDane, direccion,
                   telefono, email, rector, activo
            FROM instituciones WHERE id = @Id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<InstitucionDTO>(sql, new { Id = id });
    }

    public async Task<int> CrearAsync(InstitucionDTO dto)
    {
        const string sql = @"
            INSERT INTO instituciones
                (nombre, nit, codigo_dane, direccion, telefono, email, rector, activo)
            VALUES
                (@Nombre, @Nit, @CodigoDane, @Direccion, @Telefono, @Email, @Rector, @Activo)
            RETURNING id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, dto);
    }

    public async Task<bool> ActualizarAsync(InstitucionDTO dto)
    {
        const string sql = @"
            UPDATE instituciones
            SET nombre = @Nombre, nit = @Nit, codigo_dane = @CodigoDane,
                direccion = @Direccion, telefono = @Telefono, email = @Email,
                rector = @Rector, activo = @Activo, updated_at = NOW()
            WHERE id = @Id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync(sql, dto) > 0;
    }

    public async Task<bool> EliminarAsync(int id)
    {
        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync("DELETE FROM instituciones WHERE id = @Id;",
            new { Id = id }) > 0;
    }

    public async Task<bool> TieneSedesAsync(int institucionId)
    {
        using var cn = new NpgsqlConnection(_cs);
        var n = await cn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sedes WHERE institucion_id = @Id;",
            new { Id = institucionId });
        return n > 0;
    }

    public async Task<bool> ExisteNitAsync(string nit, int? excluirId = null)
    {
        const string sql = @"
            SELECT COUNT(*) FROM instituciones
            WHERE nit = @Nit AND (@ExcluirId IS NULL OR id <> @ExcluirId);";

        using var cn = new NpgsqlConnection(_cs);
        var n = await cn.ExecuteScalarAsync<int>(sql,
            new { Nit = nit, ExcluirId = excluirId });
        return n > 0;
    }
}