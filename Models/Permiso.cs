namespace Almacen.Models
{
    public class Permiso
    {
        public int Id { get; set; }

        public string Codigo { get; set; } = string.Empty;

        public string? Descripcion { get; set; }

        public string? Modulo { get; set; }

        public bool Activo { get; set; }
    }
}