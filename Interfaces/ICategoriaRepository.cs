using Almacen.Helpers;
using Almacen.Models;

namespace Almacen.Interfaces
{
    public interface ICategoriaRepository
    {
        Task<IEnumerable<Categoria>> ObtenerTodosAsync();

        Task<ResultadoPaginado<Categoria>> ObtenerPaginadoAsync(
            int pagina = 1,
            int tamano = 25,
            string? filtroTexto = null);

        Task<Categoria?> ObtenerPorIdAsync(int id);

        Task<int> CrearAsync(Categoria categoria);

        Task<bool> ActualizarAsync(Categoria categoria);

        Task<bool> EliminarAsync(int id);
    }
}