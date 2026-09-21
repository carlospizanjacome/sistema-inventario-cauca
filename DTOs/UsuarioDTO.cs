using System.ComponentModel.DataAnnotations;

namespace Almacen.DTOs
{
    public class UsuarioDTO
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre completo es obligatorio.")]
        public string NombreCompleto { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [EmailAddress(ErrorMessage = "Debe ingresar un correo electrónico válido.")]
        public string Email { get; set; } = string.Empty;

       
        public string Password { get; set; } = string.Empty;

        public int? RolId { get; set; }

        public bool Activo { get; set; } = true;
    }
}