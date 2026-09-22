using Almacen.DTOs;

namespace Almacen.Interfaces;

public interface IReporteRepository
{
    Task<ConsolidadoGlobalDTO> ObtenerConsolidadoGlobalAsync();
    Task<IEnumerable<ConsolidadoInstitucionDTO>> ObtenerConsolidadoInstitucionesAsync();
}