using Almacen.DTOs;
using Almacen.Helpers;
using Almacen.Interfaces;
using Dapper;
using Npgsql;

namespace Almacen.Repositories;

public class ProveedorRepository : IProveedorRepository
{
    private readonly string _cs;
    public ProveedorRepository(IConfiguration cfg)
        => _cs = cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta DefaultConnection.");

    public async Task<IEnumerable<ProveedorDTO>> ObtenerTodosAsync()
    {
        const string sql = @"
            SELECT id, nit, nombre, direccion, telefono, email, contacto,
                   observaciones, activo
            FROM proveedores
            ORDER BY nombre;";
        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<ProveedorDTO>(sql);
    }

    public async Task<ProveedorDTO?> ObtenerPorIdAsync(int id)
    {
        const string sql = @"
            SELECT id, nit, nombre, direccion, telefono, email, contacto,
                   observaciones, activo
            FROM proveedores WHERE id = @Id;";
        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<ProveedorDTO>(sql, new { Id = id });
    }

    public async Task<int> CrearAsync(ProveedorDTO dto)
    {
        const string sql = @"
            INSERT INTO proveedores
                (nit, nombre, direccion, telefono, email, contacto, observaciones, activo)
            VALUES
                (@Nit, @Nombre, @Direccion, @Telefono, @Email, @Contacto, @Observaciones, @Activo)
            RETURNING id;";
        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, dto);
    }

    public async Task<bool> ActualizarAsync(ProveedorDTO dto)
    {
        const string sql = @"
            UPDATE proveedores
            SET nit = @Nit, nombre = @Nombre, direccion = @Direccion,
                telefono = @Telefono, email = @Email, contacto = @Contacto,
                observaciones = @Observaciones, activo = @Activo,
                updated_at = NOW()
            WHERE id = @Id;";
        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync(sql, dto) > 0;
    }

    public async Task<bool> EliminarAsync(int id)
    {
        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync(
            "DELETE FROM proveedores WHERE id = @Id;", new { Id = id }) > 0;
    }

    public async Task<bool> ExisteNitAsync(string nit, int? excluirId = null)
    {
        const string sql = @"
            SELECT COUNT(*) FROM proveedores
            WHERE nit = @Nit AND (@ExcluirId IS NULL OR id <> @ExcluirId);";
        using var cn = new NpgsqlConnection(_cs);
        var n = await cn.ExecuteScalarAsync<int>(sql,
            new { Nit = nit, ExcluirId = excluirId });
        return n > 0;
    }

    public async Task<bool> TieneEntradasAsync(int proveedorId)
    {
        using var cn = new NpgsqlConnection(_cs);
        var n = await cn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM entradas WHERE proveedor_id = @Id;",
            new { Id = proveedorId });
        return n > 0;
    }

    public async Task<ResultadoPaginado<ProveedorDTO>> ObtenerPaginadoAsync(
    int pagina = 1,
    int tamano = 25,
    string? filtroTexto = null)
    {
        if (pagina < 1) pagina = 1;
        if (tamano < 1) tamano = 25;
        if (tamano > 200) tamano = 200;

        var filtroBusqueda = string.Empty;
        if (!string.IsNullOrWhiteSpace(filtroTexto))
        {
            filtroBusqueda = @" WHERE (nit ILIKE @Buscar 
                          OR nombre ILIKE @Buscar 
                          OR email ILIKE @Buscar)";
        }

        var sqlCount = $"SELECT COUNT(*) FROM proveedores {filtroBusqueda};";

        var sqlData = $@"
        SELECT id, nit, nombre, direccion, telefono, email, contacto,
               observaciones, activo
        FROM proveedores
        {filtroBusqueda}
        ORDER BY nombre
        LIMIT @Tamano OFFSET @Offset;";

        using var cn = new NpgsqlConnection(_cs);

        var parametros = new
        {
            Buscar = $"%{filtroTexto}%",
            Tamano = tamano,
            Offset = (pagina - 1) * tamano
        };

        var total = await cn.ExecuteScalarAsync<int>(sqlCount, parametros);
        var items = (await cn.QueryAsync<ProveedorDTO>(sqlData, parametros)).ToList();

        return new ResultadoPaginado<ProveedorDTO>
        {
            Items = items,
            TotalRegistros = total,
            PaginaActual = pagina,
            TamanoPagina = tamano
        };
    }
}