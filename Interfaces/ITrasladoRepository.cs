using Almacen.DTOs;
using Almacen.Helpers;

namespace Almacen.Interfaces;

public interface ITrasladoRepository
{
    Task<IEnumerable<TrasladoDTO>> ObtenerTodosAsync();
    Task<TrasladoDTO?> ObtenerPorIdAsync(int id);
    Task<int> CrearAsync(TrasladoDTO dto);

    // ═══════════════════════════════════════════════════════════
    // SOFT-DELETE (reemplaza a EliminarAsync)
    // Solo se puede anular si es el último traslado del bien.
    // ═══════════════════════════════════════════════════════════
    Task AnularAsync(int id, string motivo);

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