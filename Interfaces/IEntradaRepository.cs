using Almacen.DTOs;
using Almacen.Helpers;

namespace Almacen.Interfaces;

public interface IEntradaRepository
{
    Task<IEnumerable<EntradaDTO>> ObtenerTodasAsync();
    Task<EntradaDTO?> ObtenerPorIdAsync(int id);
    Task<int> CrearAsync(EntradaDTO dto);
    Task<bool> ActualizarAsync(EntradaDTO dto);
    Task<bool> EliminarAsync(int id);
    Task<bool> BienTieneEntradaAsync(int bienId, int? excluirId = null);
    Task<ResultadoPaginado<EntradaDTO>> ObtenerPaginadoAsync(
    int pagina = 1,
    int tamano = 25,
    string? filtroTexto = null);
}