using Almacen.DTOs;
using Almacen.Helpers;

namespace Almacen.Interfaces;

public interface IKardexRepository
{
    // ── Bienes de consumo (catálogo) ──
    Task<IEnumerable<BienConsumoDTO>> ObtenerBienesConsumoAsync();
    Task<BienConsumoDTO?> ObtenerBienConsumoPorIdAsync(int id);
    Task<int> CrearBienConsumoAsync(BienConsumoDTO dto);
    Task<bool> ActualizarBienConsumoAsync(BienConsumoDTO dto);
    Task<bool> EliminarBienConsumoAsync(int id);
    Task<bool> ExisteCodigoConsumoAsync(string codigo, int? excluirId = null);

    Task<ResultadoPaginado<BienConsumoDTO>> ObtenerBienesConsumoPaginadoAsync(
        int pagina = 1,
        int tamano = 25,
        string? filtroTexto = null,
        int? categoriaId = null,
        bool? soloBajoStock = null);

    // ── Kardex ──
    Task<IEnumerable<MovimientoConsumoDTO>> ObtenerMovimientosAsync(int? bienId = null);
    Task<IEnumerable<MovimientoConsumoDTO>> ObtenerKardexAsync(int bienId);
    Task<int> RegistrarMovimientoAsync(MovimientoConsumoDTO dto);

    // ── Alertas ──
    Task<IEnumerable<BienConsumoDTO>> ObtenerBajoStockMinimoAsync();
}