using Almacen.DTOs;
using Almacen.Helpers;

namespace Almacen.Interfaces;

public interface IEntradaRepository
{
    Task<IEnumerable<EntradaDTO>> ObtenerTodasAsync();
    Task<EntradaDTO?> ObtenerPorIdAsync(int id);
    Task<int> CrearAsync(EntradaDTO dto);
    Task<bool> ActualizarAsync(EntradaDTO dto);

    // ═══════════════════════════════════════════════════════════
    // SOFT-DELETE (reemplaza a EliminarAsync)
    // Solo se puede anular la ÚLTIMA entrada del bien.
    // ═══════════════════════════════════════════════════════════
    Task AnularAsync(int id, string motivo);

    /// <summary>
    /// Verifica si una entrada específica es la última del bien.
    /// Retorna false si hay entradas posteriores o movimientos de consumo posteriores.
    /// </summary>
    Task<bool> EsUltimaEntradaAsync(int id);

    Task<bool> BienTieneEntradaAsync(int bienId, int? excluirId = null);

    Task<ResultadoPaginado<EntradaDTO>> ObtenerPaginadoAsync(
        int pagina = 1,
        int tamano = 25,
        string? filtroTexto = null);

    Task<ResultadoPaginado<EntradaDTO>> ObtenerPaginadoConFiltrosAsync(
        FiltroMovimientoDTO filtro, int pagina = 1, int tamano = 25);
}