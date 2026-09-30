using Almacen.DTOs;
using Almacen.Models;

namespace Almacen.Interfaces;

public interface IAuditoriaRepository
{
    Task<long> InsertarAsync(Auditoria a);
    Task<AuditoriaPaginaDTO> ObtenerPaginaAsync(AuditoriaFiltro filtro);
    Task<AuditoriaResumenDTO> ObtenerResumenAsync();
    Task<IEnumerable<AuditoriaUsuarioOpcionDTO>> ObtenerUsuariosAsync();
    Task<IEnumerable<AuditoriaItemDTO>> ObtenerTodosParaExportarAsync(AuditoriaFiltro filtro);
}