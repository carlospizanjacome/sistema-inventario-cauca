using Almacen.DTOs;

namespace Almacen.Interfaces;

public interface IDepreciacionRepository
{
    Task<IEnumerable<BienDepreciacionDTO>> ObtenerResumenBienesAsync();
    Task<IEnumerable<DepreciacionDTO>> ObtenerHistoricoAsync(int bienId);
    Task<DepreciacionDTO?> ObtenerUltimoPeriodoAsync(int bienId);
    Task GuardarDepreciacionAsync(int bienId, IEnumerable<DepreciacionDTO> registros);
    Task ActualizarCamposBienAsync(int bienId, decimal depreciacionAcumulada, decimal valorNeto);
    Task<int> ContarBienesDepreciablesAsync();
}