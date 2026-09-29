using Almacen.DTOs;
using Almacen.Helpers;

namespace Almacen.Interfaces;

public interface IPrestamoRepository
{
    Task<ResultadoPaginado<PrestamoDTO>> ObtenerPaginadoAsync(
        int pagina, int tamano,
        string? filtroTexto,
        string? filtroEstado,
        DateTime? filtroFechaDesde,
        DateTime? filtroFechaHasta);

    Task<PrestamoDTO?> ObtenerPorIdAsync(int id);

    Task<int> CrearAsync(PrestamoDTO dto);

    Task AprobarAsync(int id, int funcionarioApruebaId);
    Task RechazarAsync(int id, string motivo);
    Task EntregarAsync(int id, string estadoBienEntrega, string? observaciones);
    Task DevolverAsync(int id, string estadoBienDevolucion, string? observaciones);
    Task AnularAsync(int id);
    Task EliminarAsync(int id);

    /// <summary>Marca como VENCIDO los préstamos PRESTADOS cuya fecha prevista ya pasó.</summary>
    Task<int> ActualizarVencidosAsync();

    /// <summary>KPIs para el header.</summary>
    Task<(int Activos, int Vencidos, int Devueltos, int Total)> ObtenerMetricasAsync();
}