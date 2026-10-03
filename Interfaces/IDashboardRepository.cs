using Almacen.DTOs;

namespace Almacen.Interfaces;

public interface IDashboardRepository
{
    Task<DashboardKpisDto> ObtenerKpisAsync();
    Task<IEnumerable<BienesPorCategoriaDto>> ObtenerBienesPorCategoriaAsync(int top = 10);
    Task<IEnumerable<UltimoBienDto>> ObtenerUltimosBienesAsync(int top = 5);
    Task<IEnumerable<DepreciacionMensualDto>> ObtenerDepreciacionMensualAsync(int meses = 12);
    Task<IEnumerable<TopBienDto>> ObtenerTopBienesAsync(int top = 5);
}