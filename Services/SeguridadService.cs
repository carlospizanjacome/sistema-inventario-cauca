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

        // ═══════════════════════════════════════════════════════════
        // REGLA DE NEGOCIO — Super-Admin NO ejecuta acciones operativas
        // ═══════════════════════════════════════════════════════════
        //
        // El Super-Admin administra el SISTEMA (IE, usuarios, roles, permisos)
        // pero NO opera sobre datos de una institución específica
        // (no crea bienes, entradas, traslados, etc.) porque no pertenece
        // a ninguna IE.
        //
        // Se permiten: VER, EXPORTAR (consulta transversal).
        // Se bloquean: CREAR, EDITAR, ELIMINAR, MOVIMIENTO, ABRIR, CERRAR, ESCANEAR.
        //
        // Los módulos de ADMINISTRACIÓN (instituciones, usuarios, roles,
        // permisos, vidas_utiles) NO se bloquean — el Super-Admin sí los maneja.

        private static readonly HashSet<string> ModulosAdministrativos = new(StringComparer.OrdinalIgnoreCase)
        {
            "instituciones",
            "usuarios",
            "roles",
            "permisos",
            "vidas_utiles",
            "reportes"
        };

        private static readonly HashSet<string> AccionesBloqueadas = new(StringComparer.OrdinalIgnoreCase)
        {
            "crear",
            "editar",
            "actualizar",
            "eliminar",
            "borrar",
            "movimiento",
            "abrir",
            "cerrar",
            "escanear"
        };

        /// <summary>
        /// Devuelve true si es un permiso que el Super-Admin NO debería ejercer.
        /// </summary>
        private static bool EsPermisoBloqueadoParaSuperAdmin(string codigoPermiso)
        {
            if (string.IsNullOrWhiteSpace(codigoPermiso)) return false;

            var partes = codigoPermiso.Split('.');
            if (partes.Length < 2) return false;

            var modulo = partes[0];
            var accion = partes[^1];

            // Los módulos administrativos se permiten siempre
            if (ModulosAdministrativos.Contains(modulo))
                return false;

            // Bloquear solo las acciones operativas
            return AccionesBloqueadas.Contains(accion);
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
        /// Aplica la regla de negocio de Super-Admin antes de consultar la sesión.
        /// </summary>
        public bool TienePermiso(string codigoPermiso)
        {
            // Regla: Super-Admin no ejecuta acciones operativas
            if (_sesion.EsSuperAdmin && EsPermisoBloqueadoParaSuperAdmin(codigoPermiso))
            {
                return false;
            }

            return _sesion.TienePermiso(codigoPermiso);
        }

        /// <summary>
        /// Compatibilidad: misma firma async, pero ya no consulta BD.
        /// </summary>
        public Task<bool> TienePermisoAsync(string codigoPermiso)
            => Task.FromResult(TienePermiso(codigoPermiso));

        public IReadOnlySet<string> ObtenerTodosLosPermisos()
            => _sesion.ObtenerTodosLosPermisos();
    }
}