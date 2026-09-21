using Almacen.Interfaces;

namespace Almacen.Services
{
    public class SeguridadService
    {
        private readonly UsuarioSesionService _sesion;
        private readonly IRolPermisoRepository _rolPermisoRepository;
        private readonly IPermisoRepository _permisoRepository;

        public SeguridadService(
            UsuarioSesionService sesion,
            IRolPermisoRepository rolPermisoRepository,
            IPermisoRepository permisoRepository)
        {
            _sesion = sesion;
            _rolPermisoRepository = rolPermisoRepository;
            _permisoRepository = permisoRepository;
        }

        /// <summary>
        /// Carga los permisos del usuario en la sesión. Llamar UNA SOLA VEZ al iniciar sesión.
        /// </summary>
        public async Task CargarPermisosEnSesionAsync()
        {
            if (_sesion.UsuarioActual?.RolId == null) return;

            var permisosRol = await _rolPermisoRepository
                .ObtenerPorRolAsync(_sesion.UsuarioActual.RolId.Value);

            var idsPermisos = permisosRol.Select(rp => rp.PermisoId).ToHashSet();

            var todosPermisos = await _permisoRepository.ObtenerTodosAsync();

            var codigos = todosPermisos
                .Where(p => p.Activo && idsPermisos.Contains(p.Id))
                .Select(p => p.Codigo)
                .ToList();

            _sesion.IniciarSesion(_sesion.UsuarioActual, codigos);
        }

        /// <summary>
        /// Consulta SIN tocar la BD. Instantáneo.
        /// </summary>
        public bool TienePermiso(string codigoPermiso)
            => _sesion.TienePermiso(codigoPermiso);

        /// <summary>
        /// Compatibilidad: misma firma async, pero ya no consulta BD.
        /// </summary>
        public Task<bool> TienePermisoAsync(string codigoPermiso)
            => Task.FromResult(TienePermiso(codigoPermiso));

        public IReadOnlySet<string> ObtenerTodosLosPermisos()
    => _sesion.ObtenerTodosLosPermisos();
    }
}