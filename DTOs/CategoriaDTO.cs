using System.ComponentModel.DataAnnotations;

namespace Almacen.DTOs
{
    public class CategoriaDTO
    {
        public int Id { get; set; }


        [Required(
            ErrorMessage = "El nombre es obligatorio")]
        [StringLength(
            150,
            ErrorMessage = "Máximo 150 caracteres")]
        public string Nombre { get; set; } = string.Empty;



        [StringLength(
            500,
            ErrorMessage = "Máximo 500 caracteres")]
        public string? Descripcion { get; set; }



        public bool Estado { get; set; } = true;
    }
}
