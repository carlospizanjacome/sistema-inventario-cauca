using Almacen.DTOs;
using Almacen.Helpers;

namespace Almacen.Interfaces;

public interface IImportacionRepository
{
    // ═══ Importaciones (cabecera) ═══
    Task<ResultadoPaginado<ImportacionDTO>> ObtenerPaginadoAsync(
        int pagina, int tamano, string? filtroEstado = null);

    Task<ImportacionDTO?> ObtenerPorIdAsync(int id);

    Task<int> CrearAsync(ImportacionDTO dto);

    Task ActualizarEstadoAsync(int id, string nuevoEstado);

    Task AnularAsync(int id, string motivo);

    // ═══ Detalle ═══
    Task<IEnumerable<ImportacionDetalleDTO>> ObtenerDetalleAsync(int importacionId);

    Task<int> InsertarDetalleAsync(int importacionId, IEnumerable<FilaImportacionDTO> filas);

    Task ActualizarDetalleImportadoAsync(int detalleId, int bienId);

    // ✨ NUEVOS — Para el fix del importador real
    Task MarcarDetalleImportadoPorFilaAsync(int importacionId, int numeroFila, int bienId);

    Task MarcarDetalleErrorPorFilaAsync(int importacionId, int numeroFila, string error);

    Task ActualizarContadoresAsync(int importacionId, int exitosas, int conWarning, int conError);

    // ═══ Métricas ═══
    Task<(int Total, int Completadas, int ConError)> ObtenerMetricasAsync();
}