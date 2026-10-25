using Almacen.DTOs;

namespace Almacen.Interfaces;

public interface IVidaUtilRepository
{
    Task<IEnumerable<VidaUtilDTO>> ObtenerTodosAsync();
    Task<VidaUtilDTO?> ObtenerPorIdAsync(int id);
    Task<int> CrearAsync(VidaUtilDTO dto);
    Task<bool> ActualizarAsync(VidaUtilDTO dto);
    Task<bool> EliminarAsync(int id);
    Task<int> ContarBienesAsociadosAsync(int vidaUtilId);
}