using Almacen.Models;

namespace Almacen.Interfaces
{
    public interface IPermisoRepository
    {
        Task<IEnumerable<Permiso>> ObtenerTodosAsync();

        Task<Permiso?> ObtenerPorIdAsync(int id);

        Task<int> CrearAsync(Permiso permiso);

        Task ActualizarAsync(Permiso permiso);

        Task<bool> EliminarAsync(int id);
    }
}