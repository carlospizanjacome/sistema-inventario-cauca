using Almacen.Models;

namespace Almacen.Services
{
    public class PermisoService
    {
        private readonly UsuarioSesionService _sesion;

        public PermisoService(
            UsuarioSesionService sesion)
        {
            _sesion = sesion;
        }

        public bool EsAdministrador()
        {
            return _sesion.UsuarioActual?.NombreRol == "Administrador";

        }
    }
}