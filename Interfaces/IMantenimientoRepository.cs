using Almacen.DTOs;
using Almacen.Helpers;

namespace Almacen.Interfaces;

public interface IMantenimientoRepository
{
    Task<ResultadoPaginado<MantenimientoDTO>> ObtenerPaginadoAsync(
        int pagina, int tamano,
        string? filtroTexto,
        string? filtroEstado,
        string? filtroTipo,
        DateTime? filtroFechaDesde,
        DateTime? filtroFechaHasta);

    Task<MantenimientoDTO?> ObtenerPorIdAsync(int id);
    Task<IEnumerable<MantenimientoDTO>> ObtenerPorBienAsync(int bienId);

    Task<int> CrearAsync(MantenimientoDTO dto);
    Task ActualizarAsync(MantenimientoDTO dto);
    Task CerrarAsync(int id, DateTime fechaSalida, string trabajoRealizado,
                     string? repuestos, decimal costo, string? estadoBienEgreso,
                     string? observaciones);
    Task EliminarAsync(int id);

    Task<(int Abiertos, int EnProceso, int CerradosMes, decimal CostoMes)> ObtenerMetricasAsync();
}