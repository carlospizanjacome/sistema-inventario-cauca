using Almacen.Models;

namespace Almacen.Interfaces
{
    public interface IRolPermisoRepository
    {
        Task<IEnumerable<RolPermiso>> ObtenerPorRolAsync(int rolId);

        Task AsignarPermisoAsync(int rolId, int permisoId);

        Task EliminarPermisoAsync(int rolId, int permisoId);

        /// <summary>
        /// Reemplaza TODOS los permisos de un rol en una sola transacción.
        /// Elimina los actuales y asigna los nuevos. Atomicidad total.
        /// </summary>
        Task ReemplazarPermisosAsync(int rolId, IEnumerable<int> permisoIds);
    }
}