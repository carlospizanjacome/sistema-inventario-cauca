using Almacen.DTOs;
using Almacen.Helpers;

namespace Almacen.Interfaces;

public interface ISalidaRepository
{
    Task<IEnumerable<SalidaDTO>> ObtenerTodasAsync();
    Task<SalidaDTO?> ObtenerPorIdAsync(int id);
    Task<int> CrearAsync(SalidaDTO dto);
    Task<bool> ActualizarAsync(SalidaDTO dto);
    Task<bool> EliminarAsync(int id);
    Task<bool> BienTieneSalidaAsync(int bienId, int? excluirId = null);
    Task ReactivarBienAsync(int bienId);
    Task DesactivarBienAsync(int bienId);
    Task<ResultadoPaginado<SalidaDTO>> ObtenerPaginadoAsync(
    int pagina = 1,
    int tamano = 25,
    string? filtroTexto = null);
}