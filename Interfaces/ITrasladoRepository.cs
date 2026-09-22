using Almacen.DTOs;
using Almacen.Helpers;

namespace Almacen.Interfaces;

public interface ITrasladoRepository
{
    Task<IEnumerable<TrasladoDTO>> ObtenerTodosAsync();
    Task<TrasladoDTO?> ObtenerPorIdAsync(int id);
    Task<int> CrearAsync(TrasladoDTO dto);
    Task<bool> EliminarAsync(int id);
    Task<(int? AulaId, int? FuncionarioId)> ObtenerUbicacionActualAsync(int bienId);
    Task ActualizarUbicacionBienAsync(int bienId, int aulaId, int? funcionarioId);

    Task<ResultadoPaginado<TrasladoDTO>> ObtenerPaginadoAsync(
        int pagina = 1,
        int tamano = 25,
        string? filtroTexto = null);

    Task<ResultadoPaginado<TrasladoDTO>> ObtenerPaginadoConFiltrosAsync(
        FiltroMovimientoDTO filtro,
        int pagina = 1,
        int tamano = 25);
}