using Almacen.DTOs;
using Almacen.Helpers;

namespace Almacen.Interfaces;

public interface IGarantiaRepository
{
    Task<ResultadoPaginado<GarantiaDTO>> ObtenerPaginadoAsync(
        int pagina, int tamano,
        string? filtroTexto,
        string? filtroEstado,
        string? filtroTipo);

    Task<GarantiaDTO?> ObtenerPorIdAsync(int id);
    Task<IEnumerable<GarantiaDTO>> ObtenerPorBienAsync(int bienId);

    Task<int> CrearAsync(GarantiaDTO dto);
    Task ActualizarAsync(GarantiaDTO dto);

    // ═══════════════════════════════════════════════════════════
    // SOFT-DELETE (reemplaza a EliminarAsync)
    // Solo se pueden anular garantías VENCIDAS.
    // ═══════════════════════════════════════════════════════════
    Task AnularAsync(int id, string motivo);

    Task<(int Vigentes, int PorVencer, int Vencidas, int Total)> ObtenerMetricasAsync();
}