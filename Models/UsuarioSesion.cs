namespace Almacen.Models
{
    public class UsuarioSesion
    {
        public int Id { get; set; }

        public string NombreCompleto { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public int? RolId { get; set; }

        public string? NombreRol { get; set; }

        // ⬇️ Institucion
        public int InstitucionId { get; set; }
        public bool EsSuperAdmin { get; set; }
        public string? InstitucionNombre { get; set; }

        // ⬇️ NUEVAS — Soporte multi-sede
        public int? SedeId { get; set; }
        public string? SedeNombre { get; set; }
    }
}