using Dapper;
using Almacen.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Almacen.Services;

public class AlertasIAService
{
    private readonly string _connectionString;
    private readonly ILogger<AlertasIAService> _logger;
    private readonly UsuarioSesionService _sesion;

    public AlertasIAService(
        IConfiguration configuration,
        ILogger<AlertasIAService> logger,
        UsuarioSesionService sesion)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Cadena de conexión 'DefaultConnection' no configurada.");
        _logger = logger;
        _sesion = sesion;
    }

    private async Task<NpgsqlConnection> CrearConexionAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private (string Sql, DynamicParameters Parametros) GenerarFiltro(int? sedeIdFiltro)
    {
        var sql = "";
        var p = new DynamicParameters();
        p.Add("InstitucionId", _sesion.InstitucionId);

        if (sedeIdFiltro.HasValue && sedeIdFiltro > 0)
        {
            sql = " AND sede_id = @SedeFiltro";
            p.Add("SedeFiltro", sedeIdFiltro.Value);
        }
        else if (!_sesion.VeTodasLasSedes && _sesion.SedeId.HasValue)
        {
            sql = " AND sede_id = @SedeFiltro";
            p.Add("SedeFiltro", _sesion.SedeId.Value);
        }

        return (sql, p);
    }

    public async Task<List<AlertaStockDTO>> ObtenerAlertasStockAsync(int? sedeIdFiltro = null)
    {
        var (filtro, parametros) = GenerarFiltro(sedeIdFiltro);
        var sql = $@"
            SELECT 
                bien_id AS BienId,
                codigo AS Codigo,
                nombre AS Nombre,
                stock_actual AS StockActual,
                stock_minimo AS StockMinimo,
                faltante AS Faltante,
                cpp_actual AS Cpp,
                severidad AS Severidad
            FROM v_alertas_stock
            WHERE institucion_id = @InstitucionId {filtro}
            ORDER BY 
                CASE severidad WHEN 'CRITICO' THEN 1 WHEN 'ALTO' THEN 2 ELSE 3 END,
                faltante DESC";

        await using var db = await CrearConexionAsync();
        var result = await db.QueryAsync<AlertaStockDTO>(sql, parametros);
        return result.ToList();
    }

    public async Task<List<AlertaPrestamoDTO>> ObtenerAlertasPrestamosAsync(int? sedeIdFiltro = null)
    {
        var (filtro, parametros) = GenerarFiltro(sedeIdFiltro);
        var sql = $@"
            SELECT 
                prestamo_id AS PrestamoId,
                bien_id AS BienId,
                bien_codigo AS BienCodigo,
                bien_nombre AS BienNombre,
                funcionario AS Funcionario,
                fecha_prestamo AS FechaPrestamo,
                fecha_devolucion_prevista AS FechaDevolucionPrevista,
                dias_retraso AS DiasRetraso,
                severidad AS Severidad
            FROM v_alertas_prestamos
            WHERE institucion_id = @InstitucionId {filtro}
            ORDER BY dias_retraso DESC";

        await using var db = await CrearConexionAsync();
        var result = await db.QueryAsync<AlertaPrestamoDTO>(sql, parametros);
        return result.ToList();
    }

    public async Task<List<AlertaSinResponsableDTO>> ObtenerAlertasSinResponsableAsync(int? sedeIdFiltro = null)
    {
        var (filtro, parametros) = GenerarFiltro(sedeIdFiltro);
        var sql = $@"
            SELECT 
                bien_id AS BienId,
                codigo AS Codigo,
                nombre AS Nombre,
                valor_adquisicion AS ValorAdquisicion,
                categoria AS Categoria,
                aula AS Aula
            FROM v_alertas_sin_responsable
            WHERE institucion_id = @InstitucionId {filtro}
            ORDER BY valor_adquisicion DESC";

        await using var db = await CrearConexionAsync();
        var result = await db.QueryAsync<AlertaSinResponsableDTO>(sql, parametros);
        return result.ToList();
    }

    public async Task<List<AlertaSinUbicacionDTO>> ObtenerAlertasSinUbicacionAsync(int? sedeIdFiltro = null)
    {
        var (filtro, parametros) = GenerarFiltro(sedeIdFiltro);
        var sql = $@"
            SELECT 
                bien_id AS BienId,
                codigo AS Codigo,
                nombre AS Nombre,
                valor_adquisicion AS ValorAdquisicion,
                categoria AS Categoria,
                responsable AS Responsable
            FROM v_alertas_sin_ubicacion
            WHERE institucion_id = @InstitucionId {filtro}
            ORDER BY valor_adquisicion DESC";

        await using var db = await CrearConexionAsync();
        var result = await db.QueryAsync<AlertaSinUbicacionDTO>(sql, parametros);
        return result.ToList();
    }

    public async Task RefrescarVistasAsync()
    {
        const string sql = @"
            REFRESH MATERIALIZED VIEW v_alertas_stock;
            REFRESH MATERIALIZED VIEW v_alertas_prestamos;
            REFRESH MATERIALIZED VIEW v_alertas_sin_responsable;
            REFRESH MATERIALIZED VIEW v_alertas_sin_ubicacion;";

        await using var db = await CrearConexionAsync();
        await db.ExecuteAsync(sql);
        _logger.LogInformation("Vistas de alertas refrescadas");
    }
}