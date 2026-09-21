using Almacen.DTOs;

namespace Almacen.Interfaces;

public interface ISedeRepository
{
    Task<IEnumerable<SedeDTO>> ObtenerTodasAsync();
    Task<SedeDTO?> ObtenerPorIdAsync(int id);
    Task<int> CrearAsync(SedeDTO dto);
    Task<bool> ActualizarAsync(SedeDTO dto);
    Task<bool> EliminarAsync(int id);
    Task<bool> TieneBloquesAsync(int sedeId);
}