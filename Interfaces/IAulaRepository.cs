using Almacen.DTOs;

namespace Almacen.Interfaces;

public interface IAulaRepository
{
    Task<IEnumerable<AulaDTO>> ObtenerTodasAsync();
    Task<IEnumerable<AulaDTO>> ObtenerPorBloqueAsync(int bloqueId);
    Task<AulaDTO?> ObtenerPorIdAsync(int id);
    Task<int> CrearAsync(AulaDTO dto);
    Task<bool> ActualizarAsync(AulaDTO dto);
    Task<bool> EliminarAsync(int id);
    Task<bool> TieneBienesAsync(int aulaId);
}