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

    public async Task<IEnumerable<VidaUtilDTO>> ObtenerTodosAsync()
    {
        const string sql = @"
            SELECT id, codigo_cgn AS CodigoCgn, descripcion, clase,
                   vida_util_meses AS VidaUtilMeses,
                   valor_residual_pct AS ValorResidualPct,
                   activo
            FROM vidas_utiles
            WHERE activo = TRUE
            ORDER BY codigo_cgn;";
        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<VidaUtilDTO>(sql);
    }

    public async Task<VidaUtilDTO?> ObtenerPorIdAsync(int id)
    {
        const string sql = @"
            SELECT id, codigo_cgn AS CodigoCgn, descripcion, clase,
                   vida_util_meses AS VidaUtilMeses,
                   valor_residual_pct AS ValorResidualPct,
                   activo
            FROM vidas_utiles WHERE id = @Id;";
        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<VidaUtilDTO>(sql, new { Id = id });
    }
}