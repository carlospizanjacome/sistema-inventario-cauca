using Almacen.Models;

namespace Almacen.Services
{
    public class UsuarioSesionService
    {
        public UsuarioSesion? UsuarioActual { get; private set; }

        private HashSet<string> _permisos = new(StringComparer.OrdinalIgnoreCase);

        public bool EstaAutenticado => UsuarioActual != null;

        public IReadOnlySet<string> ObtenerTodosLosPermisos() => _permisos;

        public void IniciarSesion(UsuarioSesion usuario, IEnumerable<string> permisos)
        {
            UsuarioActual = usuario;
            _permisos = new HashSet<string>(permisos, StringComparer.OrdinalIgnoreCase);
        }

        public bool TienePermiso(string codigoPermiso)
            => _permisos.Contains(codigoPermiso);

        public void CerrarSesion()
        {
            UsuarioActual = null;
            _permisos.Clear();
        }

        /// <summary>
        /// Indica si el usuario actual es super-admin (ve todas las instituciones).
        /// </summary>
        public bool EsSuperAdmin => UsuarioActual?.EsSuperAdmin ?? false;

        /// <summary>
        /// Institución del usuario actual. 0 si no hay sesión.
        /// </summary>
        public int InstitucionId => UsuarioActual?.InstitucionId ?? 0;

        public string? InstitucionNombre => UsuarioActual?.InstitucionNombre;

    }
}