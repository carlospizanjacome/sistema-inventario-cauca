using Almacen.DTOs;
using Almacen.Helpers;
using Almacen.Models;

namespace Almacen.Interfaces;

public interface IBienRepository
{
    Task<IEnumerable<Bien>> ObtenerTodosAsync();
    Task<Bien?> ObtenerPorIdAsync(int id);
    Task<int> CrearAsync(Bien bien);
    Task ActualizarAsync(Bien bien);
    Task<bool> EliminarAsync(int id);

    Task<ResultadoPaginado<Bien>> ObtenerPaginadoAsync(
        int pagina = 1,
        int tamano = 25,
        string? filtroTexto = null);

    Task<ResultadoPaginado<Bien>> ObtenerPaginadoConFiltrosAsync(
        FiltroBienDTO filtro,
        int pagina = 1,
        int tamano = 25);

    Task<IEnumerable<Bien>> ObtenerPorFuncionarioAsync(int funcionarioId);

    // Verificar si el bien tiene historia antes de eliminar
    Task<CompromisoDTO> VerificarCompromisoAsync(int bienId);

    // ✨ NUEVOS — Verificación de duplicados por código contable
    Task<Bien?> ObtenerPorCodigoAsync(string codigo);
    Task<bool> ExisteCodigoAsync(string codigo);
}