using Almacen.DTOs;
using Almacen.Helpers;

namespace Almacen.Interfaces;

public interface IFuncionarioRepository
{
    Task<IEnumerable<FuncionarioDTO>> ObtenerTodosAsync();
    Task<FuncionarioDTO?> ObtenerPorIdAsync(int id);
    Task<int> CrearAsync(FuncionarioDTO dto);
    Task<bool> ActualizarAsync(FuncionarioDTO dto);
    Task<bool> EliminarAsync(int id);
    Task<bool> ExisteCedulaAsync(string cedula, int? excluirId = null);
    Task<bool> TieneBienesAsync(int funcionarioId);

    Task<ResultadoPaginado<FuncionarioDTO>> ObtenerPaginadoAsync(
    int pagina = 1,
    int tamano = 25,
    string? filtroTexto = null);

    Task<ResultadoPaginado<FuncionarioCuentandanteDTO>> ObtenerCuentandantesPaginadoAsync(
    int pagina = 1,
    int tamano = 25,
    string? filtroTexto = null);
}