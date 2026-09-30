using Almacen.DTOs;

namespace Almacen.Interfaces;

public interface IReporteNormativoRepository
{
    Task<IEnumerable<ReporteNormativoDTO>> ObtenerTodosAsync(
        string? filtroTexto = null,
        string? filtroEstado = null,
        string? filtroPeriodicidad = null);

    Task<ReporteNormativoDTO?> ObtenerPorIdAsync(int id);

    Task<int> CrearAsync(ReporteNormativoDTO dto);
    Task ActualizarAsync(ReporteNormativoDTO dto);
    Task CambiarEstadoAsync(int id, string nuevoEstado);
    Task EliminarAsync(int id);
    Task ActualizarPlantillaAsync(int id, string? rutaPlantilla);
    Task<(int Borradores, int Vigentes, int Retirados, int Total)> ObtenerMetricasAsync();
}