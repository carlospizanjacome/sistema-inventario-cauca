using Almacen.DTOs;

namespace Almacen.Interfaces;

public interface IInstitucionRepository
{
    Task<IEnumerable<InstitucionDTO>> ObtenerTodasAsync();
    Task<InstitucionDTO?> ObtenerPorIdAsync(int id);
    Task<int> CrearAsync(InstitucionDTO dto);
    Task<bool> ActualizarAsync(InstitucionDTO dto);
    Task<bool> EliminarAsync(int id);
    Task<bool> TieneSedesAsync(int institucionId);
    Task<bool> ExisteNitAsync(string nit, int? excluirId = null);
}