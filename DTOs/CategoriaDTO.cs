using System.ComponentModel.DataAnnotations;

namespace Almacen.DTOs
{
    public class CategoriaDTO
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(150, ErrorMessage = "Máximo 150 caracteres")]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Máximo 500 caracteres")]
        public string? Descripcion { get; set; }

        [StringLength(10, ErrorMessage = "El código CGN no puede tener más de 10 caracteres")]
        public string? CodigoCgn { get; set; }

        // ✨ NUEVO
        public string TipoBien { get; set; } = "devolutivo";

        public bool Estado { get; set; } = true;
    }
}