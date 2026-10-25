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

    public async Task<int> CrearAsync(VidaUtilDTO dto)
    {
        const string sql = @"
            INSERT INTO catalogo_cgn 
                (codigo_cgn, descripcion, clase, vida_util_meses, valor_residual_pct, activo, tipo)
            VALUES 
                (@CodigoCgn, @Descripcion, @Clase, @VidaUtilMeses, @ValorResidualPct, TRUE, 'ACTIVO')
            RETURNING id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, dto);
    }

    public async Task<bool> ActualizarAsync(VidaUtilDTO dto)
    {
        const string sql = @"
            UPDATE catalogo_cgn 
            SET codigo_cgn = @CodigoCgn,
                descripcion = @Descripcion,
                clase = @Clase,
                vida_util_meses = @VidaUtilMeses,
                valor_residual_pct = @ValorResidualPct
            WHERE id = @Id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync(sql, dto) > 0;
    }

    public async Task<bool> EliminarAsync(int id)
    {
        const string sql = @"DELETE FROM catalogo_cgn WHERE id = @Id;";
        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync(sql, new { Id = id }) > 0;
    }

    public async Task<int> ContarBienesAsociadosAsync(int vidaUtilId)
    {
        const string sql = "SELECT COUNT(*) FROM bienes WHERE vida_util_id = @Id;";
        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, new { Id = vidaUtilId });
    }
}