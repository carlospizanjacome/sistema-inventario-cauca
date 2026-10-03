using Almacen.DTOs;
using Almacen.Helpers;

namespace Almacen.Interfaces;

public interface ITomaFisicaRepository
{
    // ── Tomas físicas (cabecera) ──
    Task<IEnumerable<TomaFisicaDTO>> ObtenerTodasAsync();
    Task<ResultadoPaginado<TomaFisicaDTO>> ObtenerPaginadoAsync(
        int pagina = 1,
        int tamano = 25,
        string? filtroTexto = null,
        string? estado = null);
    Task<TomaFisicaDTO?> ObtenerPorIdAsync(int id);
    Task<int> CrearAsync(TomaFisicaDTO dto);
    Task<bool> ActualizarAsync(TomaFisicaDTO dto);
    Task<bool> EliminarAsync(int id);
    Task<bool> ExisteCodigoAsync(string codigo, int? excluirId = null);

    // ── Ciclo de vida ──
    Task<int> AbrirAsync(int tomaId);
    Task<bool> CerrarAsync(int tomaId);

    /// <summary>
    /// ANULAR toma: soft-delete. Solo si está CERRADA.
    /// </summary>
    Task<bool> AnularAsync(int id, string motivo);

    // ── Detalle ──
    Task<IEnumerable<TomaFisicaDetalleDTO>> ObtenerDetalleAsync(int tomaId);
    Task<TomaFisicaDetalleDTO?> ObtenerDetallePorIdAsync(int detalleId);
    Task<TomaFisicaDetalleDTO?> BuscarPorCodigoAsync(int tomaId, string codigo);

    // ── Escaneo ──
    Task<(bool Ok, string Mensaje, string Tipo)> EscanearAsync(int tomaId, EscaneoDTO dto, int usuarioId);
    Task<bool> RequiereConfirmacionSobranteAsync(int tomaId, string codigo);

    // ── Reporte ──
    Task<ReporteTomaFisicaDTO?> ObtenerReporteAsync(int tomaId);
}