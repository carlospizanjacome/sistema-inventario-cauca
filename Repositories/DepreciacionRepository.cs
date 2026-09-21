using Almacen.DTOs;
using Almacen.Helpers;
using Almacen.Interfaces;
using Almacen.Services;
using Dapper;
using Npgsql;

namespace Almacen.Repositories;

public class DepreciacionRepository : IDepreciacionRepository
{
    private readonly string _cs;
    private readonly UsuarioSesionService _sesion;

    public DepreciacionRepository(IConfiguration cfg, UsuarioSesionService sesion)
    {
        _cs = cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta DefaultConnection.");
        _sesion = sesion;
    }

    public async Task<IEnumerable<BienDepreciacionDTO>> ObtenerResumenBienesAsync()
    {
        var (filtro, param) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "b");

        var sql = $@"
            SELECT
                b.id                       AS BienId,
                b.codigo,
                b.nombre,
                c.nombre                   AS CategoriaNombre,
                b.fecha_adquisicion::timestamp AS FechaAdquisicion,
                v.vida_util_meses          AS VidaUtilMeses,
                b.valor_adquisicion        AS ValorInicial,
                b.valor_residual           AS ValorResidual,
                b.depreciacion_acumulada   AS DepreciacionAcumulada,
                b.valor_neto               AS ValorNeto
            FROM bienes b
            LEFT JOIN categorias c ON c.id = b.categoria_id
            LEFT JOIN vidas_utiles v ON v.id = b.vida_util_id
            WHERE b.activo = TRUE
              AND b.tipo_bien = 'devolutivo'
              AND b.vida_util_id IS NOT NULL
              AND b.fecha_adquisicion IS NOT NULL
              {filtro}
            ORDER BY b.nombre;";

        using var cn = new NpgsqlConnection(_cs);
        var lista = (await cn.QueryAsync<BienDepreciacionDTO>(sql, param)).ToList();

        // Calcular campos derivados en memoria
        var hoy = DateTime.Today;
        foreach (var b in lista)
        {
            var meses = b.VidaUtilMeses ?? 0;
            b.DepreciacionMensual = meses > 0
                ? Math.Round((b.ValorInicial - b.ValorResidual) / meses, 2)
                : 0;

            b.MesesTranscurridos = b.FechaAdquisicion.HasValue
                ? ((hoy.Year - b.FechaAdquisicion.Value.Year) * 12)
                  + (hoy.Month - b.FechaAdquisicion.Value.Month) + 1
                : 0;

            var valorADepreciar = b.ValorInicial - b.ValorResidual;
            b.PorcentajeDepreciado = valorADepreciar > 0
                ? Math.Round(b.DepreciacionAcumulada / valorADepreciar * 100, 2)
                : 0;

            b.CompletamenteDepreciado = b.DepreciacionAcumulada >= valorADepreciar;
        }

        return lista;
    }

    public async Task<IEnumerable<DepreciacionDTO>> ObtenerHistoricoAsync(int bienId)
    {
        const string sql = @"
            SELECT id, bien_id AS BienId, periodo_anio AS PeriodoAnio,
                   periodo_mes AS PeriodoMes, valor_inicial AS ValorInicial,
                   valor_residual AS ValorResidual,
                   depreciacion_mes AS DepreciacionMes,
                   depreciacion_acumulada AS DepreciacionAcumulada,
                   valor_neto AS ValorNeto
            FROM depreciacion
            WHERE bien_id = @BienId
            ORDER BY periodo_anio DESC, periodo_mes DESC;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<DepreciacionDTO>(sql, new { BienId = bienId });
    }

    public async Task<DepreciacionDTO?> ObtenerUltimoPeriodoAsync(int bienId)
    {
        const string sql = @"
            SELECT id, bien_id AS BienId, periodo_anio AS PeriodoAnio,
                   periodo_mes AS PeriodoMes, valor_inicial AS ValorInicial,
                   valor_residual AS ValorResidual,
                   depreciacion_mes AS DepreciacionMes,
                   depreciacion_acumulada AS DepreciacionAcumulada,
                   valor_neto AS ValorNeto
            FROM depreciacion
            WHERE bien_id = @BienId
            ORDER BY periodo_anio DESC, periodo_mes DESC
            LIMIT 1;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<DepreciacionDTO>(sql, new { BienId = bienId });
    }

    public async Task GuardarDepreciacionAsync(int bienId, IEnumerable<DepreciacionDTO> registros)
    {
        const string sql = @"
            INSERT INTO depreciacion
                (bien_id, periodo_anio, periodo_mes, valor_inicial, valor_residual,
                 depreciacion_mes, depreciacion_acumulada, valor_neto)
            VALUES
                (@BienId, @PeriodoAnio, @PeriodoMes, @ValorInicial, @ValorResidual,
                 @DepreciacionMes, @DepreciacionAcumulada, @ValorNeto)
            ON CONFLICT (bien_id, periodo_anio, periodo_mes) DO NOTHING;";

        using var cn = new NpgsqlConnection(_cs);
        await cn.ExecuteAsync(sql, registros);
    }

    public async Task ActualizarCamposBienAsync(int bienId, decimal depreciacionAcumulada, decimal valorNeto)
    {
        const string sql = @"
            UPDATE bienes
            SET depreciacion_acumulada = @Dep,
                valor_neto = @ValorNeto,
                fecha_ultimo_calculo = NOW(),
                updated_at = NOW()
            WHERE id = @Id;";

        using var cn = new NpgsqlConnection(_cs);
        await cn.ExecuteAsync(sql, new
        {
            Id = bienId,
            Dep = depreciacionAcumulada,
            ValorNeto = valorNeto
        });
    }

    public async Task<int> ContarBienesDepreciablesAsync()
    {
        var (filtro, param) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "b");

        var sql = $@"
            SELECT COUNT(*) FROM bienes b
            WHERE b.activo = TRUE
              AND b.tipo_bien = 'devolutivo'
              AND b.vida_util_id IS NOT NULL
              AND b.fecha_adquisicion IS NOT NULL
              {filtro};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, param);
    }
}