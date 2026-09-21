using Almacen.Models;

namespace Almacen.Interfaces
{
    public interface IRolRepository
    {
        Task<IEnumerable<Rol>> ObtenerTodosAsync();

        Task<Rol?> ObtenerPorIdAsync(int id);

        Task<int> CrearAsync(Rol rol);

        Task ActualizarAsync(Rol rol);

        Task<bool> EliminarAsync(int id);
    }
}