using Almacen.DTOs;
using Almacen.Interfaces;
using Dapper;
using Npgsql;

namespace Almacen.Repositories;

public class VidaUtilRepository : IVidaUtilRepository
{
    private readonly string _cs;

    public VidaUtilRepository(IConfiguration cfg)
        => _cs = cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta DefaultConnection.");

    private const string BaseSelect = @"
        SELECT
            id,
            codigo_cgn AS CodigoCgn,
            descripcion,
            clase,
            vida_util_meses AS VidaUtilMeses,
            valor_residual_pct AS ValorResidualPct,
            activo,
            tipo
        FROM catalogo_cgn";

    public async Task<IEnumerable<VidaUtilDTO>> ObtenerTodosAsync()
    {
        var sql = $@"{BaseSelect}
            WHERE activo = TRUE
            ORDER BY tipo DESC, codigo_cgn;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<VidaUtilDTO>(sql);
    }

    public async Task<VidaUtilDTO?> ObtenerPorIdAsync(int id)
    {
        var sql = $"{BaseSelect} WHERE id = @Id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<VidaUtilDTO>(sql, new { Id = id });
    }
}