using Almacen.DTOs;

namespace Almacen.Interfaces;

public interface IBloqueRepository
{
    Task<IEnumerable<BloqueDTO>> ObtenerTodosAsync();
    Task<IEnumerable<BloqueDTO>> ObtenerPorSedeAsync(int sedeId);
    Task<BloqueDTO?> ObtenerPorIdAsync(int id);
    Task<int> CrearAsync(BloqueDTO dto);
    Task<bool> ActualizarAsync(BloqueDTO dto);
    Task<bool> EliminarAsync(int id);
    Task<bool> TieneAulasAsync(int bloqueId);
}