using Almacen.Models;

namespace Almacen.Services
{
    public class UsuarioSesionService
    {
        private const string ROL_RECTOR_NOMBRE = "Rector";

        public UsuarioSesion? UsuarioActual { get; private set; }

        private HashSet<string> _permisos = new(StringComparer.OrdinalIgnoreCase);

        // ═══ NUEVO: Variables para el filtro global de sede ═══
        private int? _sedeFiltroGlobalId;
        private string? _sedeFiltroGlobalNombre;
        // ══════════════════════════════════════════════════════

        public bool EstaAutenticado => UsuarioActual != null;

        public IReadOnlySet<string> ObtenerTodosLosPermisos() => _permisos;

        public void IniciarSesion(UsuarioSesion usuario, IEnumerable<string> permisos)
        {
            UsuarioActual = usuario;
            _permisos = new HashSet<string>(permisos, StringComparer.OrdinalIgnoreCase);

            // Al iniciar sesión, reseteamos el filtro global
            _sedeFiltroGlobalId = null;
            _sedeFiltroGlobalNombre = null;
        }

        public bool TienePermiso(string codigoPermiso)
            => _permisos.Contains(codigoPermiso);

        public void CerrarSesion()
        {
            UsuarioActual = null;
            _permisos.Clear();
            _sedeFiltroGlobalId = null;
            _sedeFiltroGlobalNombre = null;
        }

        public bool EsSuperAdmin => UsuarioActual?.EsSuperAdmin ?? false;

        public int InstitucionId => UsuarioActual?.InstitucionId ?? 0;

        public string? InstitucionNombre => UsuarioActual?.InstitucionNombre;

        // ═══ NUEVO: Lógica del filtro global de sede ═══
        // La sede real asignada al usuario en la BD
        public int? UsuarioSedeId => UsuarioActual?.SedeId;

        // La sede efectiva a filtrar (si el Rector seleccionó una en el combo, o la del usuario)
        public int? SedeId => _sedeFiltroGlobalId ?? UsuarioActual?.SedeId;
        public string? SedeNombre => _sedeFiltroGlobalId.HasValue ? _sedeFiltroGlobalNombre : UsuarioActual?.SedeNombre;

        // ¿Tiene permiso para usar el combo y ver "Todas"?
        public bool PuedeVerTodasLasSedes => EsSuperAdmin || EsRector || UsuarioActual?.SedeId == null;

        // Regla que usan los repositorios: Si el Rector selecciona una sede, esta será 'false' 
        // y los repos filtrarán automáticamente por el SedeId de arriba.
        public bool VeTodasLasSedes => PuedeVerTodasLasSedes && _sedeFiltroGlobalId == null;

        // Método para cambiar la sede desde el MainLayout
        public void CambiarSedeFiltro(int? sedeId, string? sedeNombre)
        {
            _sedeFiltroGlobalId = sedeId;
            _sedeFiltroGlobalNombre = sedeNombre;
        }
        // ══════════════════════════════════════════════════════

        public bool EsRector
            => string.Equals(UsuarioActual?.NombreRol?.Trim(),
                             ROL_RECTOR_NOMBRE,
                             StringComparison.OrdinalIgnoreCase);
    }
}