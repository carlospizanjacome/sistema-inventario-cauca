using Almacen.Helpers;
using Almacen.Models;

namespace Almacen.Interfaces
{
    public interface IBienRepository
    {
        Task<IEnumerable<Bien>> ObtenerTodosAsync();

    Task<Bien?> ObtenerPorIdAsync(int id);

        Task<int> CrearAsync(Bien bien);

        Task ActualizarAsync(Bien bien);

        Task<bool> EliminarAsync(int id);

        Task<ResultadoPaginado<Bien>> ObtenerPaginadoAsync(
        int pagina = 1,
        int tamano = 25,
        string? filtroTexto = null);

        Task<IEnumerable<Bien>> ObtenerPorFuncionarioAsync(int funcionarioId);
    }

}
