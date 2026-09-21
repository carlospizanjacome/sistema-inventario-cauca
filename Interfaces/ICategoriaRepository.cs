using Almacen.Models;

namespace Almacen.Interfaces
{
    public interface ICategoriaRepository
    {
        Task<IEnumerable<Categoria>> ObtenerTodosAsync();

        Task<Categoria?> ObtenerPorIdAsync(int id);

        Task<int> CrearAsync(Categoria categoria);

        Task<bool> ActualizarAsync(Categoria categoria);

        Task<bool> EliminarAsync(int id);
    }
}