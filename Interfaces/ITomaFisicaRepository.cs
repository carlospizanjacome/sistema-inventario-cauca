
using Almacen.DTOs;

namespace Almacen.Interfaces;

public interface ITomaFisicaRepository
{
    // ── Tomas físicas (cabecera) ──
    Task<IEnumerable<TomaFisicaDTO>> ObtenerTodasAsync();
    Task<TomaFisicaDTO?> ObtenerPorIdAsync(int id);
    Task<int> CrearAsync(TomaFisicaDTO dto);
    Task<bool> ActualizarAsync(TomaFisicaDTO dto);
    Task<bool> EliminarAsync(int id);
    Task<bool> ExisteCodigoAsync(string codigo, int? excluirId = null);

    // ── Ciclo de vida ──
    /// <summary>
    /// ABRIR toma: congela el inventario actual (snapshot) y cambia estado a EN_CURSO.
    /// </summary>
    Task<int> AbrirAsync(int tomaId);

    /// <summary>
    /// CERRAR toma: marca faltantes los pendientes, calcula totales y cambia estado a CERRADA.
    /// </summary>
    Task<bool> CerrarAsync(int tomaId);

    // ── Detalle ──
    Task<IEnumerable<TomaFisicaDetalleDTO>> ObtenerDetalleAsync(int tomaId);
    Task<TomaFisicaDetalleDTO?> ObtenerDetallePorIdAsync(int detalleId);
    Task<TomaFisicaDetalleDTO?> BuscarPorCodigoAsync(int tomaId, string codigo);

    // ── Escaneo ──
    Task<(bool Ok, string Mensaje, string Tipo)> EscanearAsync(int tomaId, EscaneoDTO dto, int usuarioId);

    /// <summary>
    /// Determina si el código NO está en el snapshot (o sea, va a ser SOBRANTE).
    /// En ese caso, el sistema debe pedir confirmación al auditor.
    /// </summary>
    Task<bool> RequiereConfirmacionSobranteAsync(int tomaId, string codigo);

    // ── Reporte ──
    Task<ReporteTomaFisicaDTO?> ObtenerReporteAsync(int tomaId);
}