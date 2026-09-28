using Almacen.DTOs;
using Almacen.Helpers;

namespace Almacen.Interfaces;

public interface IBloqueRepository
{
    Task<IEnumerable<BloqueDTO>> ObtenerTodosAsync();

    Task<ResultadoPaginado<BloqueDTO>> ObtenerPaginadoAsync(
        int pagina = 1,
        int tamano = 25,
        string? filtroTexto = null,
        int? sedeId = null);

    Task<IEnumerable<BloqueDTO>> ObtenerPorSedeAsync(int sedeId);
    Task<BloqueDTO?> ObtenerPorIdAsync(int id);
    Task<int> CrearAsync(BloqueDTO dto);
    Task<bool> ActualizarAsync(BloqueDTO dto);
    Task<bool> EliminarAsync(int id);
    Task<bool> TieneAulasAsync(int bloqueId);
}