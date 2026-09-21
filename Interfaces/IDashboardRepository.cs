using Almacen.DTOs;

namespace Almacen.Interfaces;

public interface IDashboardRepository
{
    Task<DashboardKpisDto> ObtenerKpisAsync();
    Task<IEnumerable<BienesPorCategoriaDto>> ObtenerBienesPorCategoriaAsync(int top = 5);
    Task<IEnumerable<UltimoBienDto>> ObtenerUltimosBienesAsync(int top = 5);
}