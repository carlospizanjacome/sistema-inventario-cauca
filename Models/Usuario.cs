namespace Almacen.Models
{
    public class Usuario
    {
        public int Id { get; set; }

        public string NombreCompleto { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public bool Activo { get; set; }

        public int? RolId { get; set; }

        public string? NombreRol { get; set; }

        public int InstitucionId { get; set; }
        public bool EsSuperAdmin { get; set; }

        public string? InstitucionNombre { get; set; }   // opcional, para mostrar
    }
}