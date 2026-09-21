using System.ComponentModel.DataAnnotations;

namespace Almacen.DTOs
{
    public class PermisoDTO
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El código del permiso es obligatorio.")]
        public string Codigo { get; set; } = string.Empty;

        public string? Descripcion { get; set; }

        [Required(ErrorMessage = "El módulo es obligatorio.")]
        public string Modulo { get; set; } = string.Empty;

        public bool Activo { get; set; } = true;
    }
}