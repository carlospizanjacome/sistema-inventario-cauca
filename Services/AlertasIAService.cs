using Dapper;
using Almacen.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Almacen.Services;

/// <summary>
/// Servicio de Alertas IA - Fase 2 (100% C#)
/// Consulta vistas materializadas de alertas.
/// </summary>
public class AlertasIAService
{
    private readonly string _connectionString;
    private readonly ILogger<AlertasIAService> _logger;

    public AlertasIAService(IConfiguration configuration, ILogger<AlertasIAService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Cadena de conexión 'DefaultConnection' no configurada.");
        _logger = logger;
    }

    private async Task<NpgsqlConnection> CrearConexionAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    // ═══════════════════════════════════════════════════════════
    // 1. ALERTAS DE STOCK CRÍTICO
    // ═══════════════════════════════════════════════════════════
    public async Task<List<AlertaStockDTO>> ObtenerAlertasStockAsync(int institucionId)
    {
        const string sql = @"
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
            WHERE institucion_id = @InstitucionId
            ORDER BY 
                CASE severidad WHEN 'CRITICO' THEN 1 WHEN 'ALTO' THEN 2 ELSE 3 END,
                faltante DESC";

        await using var db = await CrearConexionAsync();
        var result = await db.QueryAsync<AlertaStockDTO>(sql, new { InstitucionId = institucionId });
        return result.ToList();
    }

    // ═══════════════════════════════════════════════════════════
    // 2. PRÉSTAMOS VENCIDOS
    // ═══════════════════════════════════════════════════════════
    public async Task<List<AlertaPrestamoDTO>> ObtenerAlertasPrestamosAsync(int institucionId)
    {
        const string sql = @"
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
            WHERE institucion_id = @InstitucionId
            ORDER BY dias_retraso DESC";

        await using var db = await CrearConexionAsync();
        var result = await db.QueryAsync<AlertaPrestamoDTO>(sql, new { InstitucionId = institucionId });
        return result.ToList();
    }

    // ═══════════════════════════════════════════════════════════
    // 3. SIN RESPONSABLE
    // ═══════════════════════════════════════════════════════════
    public async Task<List<AlertaSinResponsableDTO>> ObtenerAlertasSinResponsableAsync(int institucionId)
    {
        const string sql = @"
            SELECT 
                bien_id AS BienId,
                codigo AS Codigo,
                nombre AS Nombre,
                valor_adquisicion AS ValorAdquisicion,
                categoria AS Categoria,
                aula AS Aula
            FROM v_alertas_sin_responsable
            WHERE institucion_id = @InstitucionId
            ORDER BY valor_adquisicion DESC";

        await using var db = await CrearConexionAsync();
        var result = await db.QueryAsync<AlertaSinResponsableDTO>(sql, new { InstitucionId = institucionId });
        return result.ToList();
    }

    // ═══════════════════════════════════════════════════════════
    // 4. SIN UBICACIÓN
    // ═══════════════════════════════════════════════════════════
    public async Task<List<AlertaSinUbicacionDTO>> ObtenerAlertasSinUbicacionAsync(int institucionId)
    {
        const string sql = @"
            SELECT 
                bien_id AS BienId,
                codigo AS Codigo,
                nombre AS Nombre,
                valor_adquisicion AS ValorAdquisicion,
                categoria AS Categoria,
                responsable AS Responsable
            FROM v_alertas_sin_ubicacion
            WHERE institucion_id = @InstitucionId
            ORDER BY valor_adquisicion DESC";

        await using var db = await CrearConexionAsync();
        var result = await db.QueryAsync<AlertaSinUbicacionDTO>(sql, new { InstitucionId = institucionId });
        return result.ToList();
    }

    // ═══════════════════════════════════════════════════════════
    // 5. REFRESCAR VISTAS
    // ═══════════════════════════════════════════════════════════
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