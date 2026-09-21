using Almacen.DTOs;

namespace Almacen.Interfaces;

public interface IVidaUtilRepository
{
    Task<IEnumerable<VidaUtilDTO>> ObtenerTodosAsync();
    Task<VidaUtilDTO?> ObtenerPorIdAsync(int id);
}