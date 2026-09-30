using Almacen.DTOs;

namespace Almacen.Interfaces;

public interface IConciliacionRepository
{
    Task<IEnumerable<TomaFisicaCerradaDTO>> ObtenerTomasCerradasAsync();
    Task<ConciliacionGlobalDTO?> ObtenerGlobalAsync(int tomaFisicaId);
    Task<IEnumerable<ConciliacionCategoriaDTO>> ObtenerPorCategoriaAsync(int tomaFisicaId);
    Task<IEnumerable<ConciliacionDetalleDTO>> ObtenerDetalleAsync(int tomaFisicaId);
}