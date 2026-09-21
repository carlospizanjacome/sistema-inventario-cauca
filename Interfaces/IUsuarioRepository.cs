using Almacen.Models;

namespace Almacen.Interfaces
{
    public interface IUsuarioRepository
    {
        Task<IEnumerable<Usuario>> ObtenerTodosAsync();
        Task<IEnumerable<Usuario>> ObtenerPorInstitucionAsync(int institucionId);
        Task<Usuario?> ObtenerPorIdAsync(int id);
        Task<Usuario?> ObtenerPorEmailAsync(string email);
        Task<int> CrearAsync(Usuario usuario);
        Task ActualizarAsync(Usuario usuario);
        Task<bool> EliminarAsync(int id);
        Task<bool> ExisteEmailAsync(string email, int? excluirId = null);
    }
}