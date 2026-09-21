using Almacen.DTOs;
using Almacen.Helpers;

namespace Almacen.Interfaces;

public interface IProveedorRepository
{
    Task<IEnumerable<ProveedorDTO>> ObtenerTodosAsync();
    Task<ProveedorDTO?> ObtenerPorIdAsync(int id);
    Task<int> CrearAsync(ProveedorDTO dto);
    Task<bool> ActualizarAsync(ProveedorDTO dto);
    Task<bool> EliminarAsync(int id);
    Task<bool> ExisteNitAsync(string nit, int? excluirId = null);
    Task<bool> TieneEntradasAsync(int proveedorId);

    Task<ResultadoPaginado<ProveedorDTO>> ObtenerPaginadoAsync(
    int pagina = 1,
    int tamano = 25,
    string? filtroTexto = null);
}