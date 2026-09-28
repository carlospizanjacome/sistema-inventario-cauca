using Almacen.DTOs;
using Almacen.Helpers;

namespace Almacen.Interfaces;

public interface IAulaRepository
{
    Task<IEnumerable<AulaDTO>> ObtenerTodasAsync();

    Task<ResultadoPaginado<AulaDTO>> ObtenerPaginadoAsync(
        int pagina = 1,
        int tamano = 25,
        string? filtroTexto = null,
        int? bloqueId = null,
        string? tipo = null);

    Task<IEnumerable<AulaDTO>> ObtenerPorBloqueAsync(int bloqueId);
    Task<AulaDTO?> ObtenerPorIdAsync(int id);
    Task<int> CrearAsync(AulaDTO dto);
    Task<bool> ActualizarAsync(AulaDTO dto);
    Task<bool> EliminarAsync(int id);
    Task<bool> TieneBienesAsync(int aulaId);
}