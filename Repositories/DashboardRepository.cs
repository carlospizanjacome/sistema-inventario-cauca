using Almacen.DTOs;
using Almacen.Interfaces;
using Dapper;
using Npgsql;

namespace Almacen.Repositories;

public class DashboardRepository : IDashboardRepository
{
    private readonly string _connectionString;

    public DashboardRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta 'DefaultConnection' en configuración.");
    }

    public async Task<DashboardKpisDto> ObtenerKpisAsync()
    {
        const string sql = @"
            SELECT
                (SELECT COUNT(*) FROM bienes     WHERE activo = true)  AS TotalBienes,
                (SELECT COUNT(*) FROM categorias WHERE estado = true)  AS TotalCategorias,
                (SELECT COUNT(*) FROM usuarios   WHERE activo = true)  AS UsuariosActivos,
                (SELECT COUNT(*) FROM roles)                            AS TotalRoles,
                (SELECT COALESCE(SUM(valor_adquisicion), 0)
                   FROM bienes WHERE activo = true)                     AS ValorInventario;
        ";

        using var connection = new NpgsqlConnection(_connectionString);
        return await connection.QuerySingleAsync<DashboardKpisDto>(sql);
    }

    public async Task<IEnumerable<BienesPorCategoriaDto>> ObtenerBienesPorCategoriaAsync(int top = 5)
    {
        const string sql = @"
            SELECT
                c.id                                   AS CategoriaId,
                c.nombre                               AS CategoriaNombre,
                COUNT(b.id)                            AS Cantidad,
                COALESCE(SUM(b.valor_adquisicion), 0)  AS ValorTotal
            FROM categorias c
            LEFT JOIN bienes b
                   ON b.categoria_id = c.id
                  AND b.activo = true
            WHERE c.estado = true
            GROUP BY c.id, c.nombre
            ORDER BY Cantidad DESC, c.nombre ASC
            LIMIT @Top;
        ";

        using var connection = new NpgsqlConnection(_connectionString);
        return await connection.QueryAsync<BienesPorCategoriaDto>(sql, new { Top = top });
    }

    public async Task<IEnumerable<UltimoBienDto>> ObtenerUltimosBienesAsync(int top = 5)
    {
        const string sql = @"
            SELECT
                b.id                              AS Id,
                COALESCE(b.codigo, '—')           AS Codigo,
                b.nombre                          AS Nombre,
                COALESCE(c.nombre, '—')           AS CategoriaNombre,
                COALESCE(b.valor_adquisicion, 0)  AS Valor,
                b.created_at                      AS FechaRegistro
            FROM bienes b
            LEFT JOIN categorias c ON c.id = b.categoria_id
            WHERE b.activo = true
            ORDER BY b.created_at DESC NULLS LAST, b.id DESC
            LIMIT @Top;
        ";

        using var connection = new NpgsqlConnection(_connectionString);
        return await connection.QueryAsync<UltimoBienDto>(sql, new { Top = top });
    }
}