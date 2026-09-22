namespace Almacen.DTOs;

/// <summary>
/// Filtros para Entradas, Salidas y Traslados (compartido).
/// </summary>
public class FiltroMovimientoDTO
{
    public string? Texto { get; set; }
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public string? Tipo { get; set; }

    public bool HayFiltros =>
        !string.IsNullOrWhiteSpace(Texto) ||
        FechaDesde.HasValue ||
        FechaHasta.HasValue ||
        !string.IsNullOrWhiteSpace(Tipo);

    public int CantidadFiltros
    {
        get
        {
            var n = 0;
            if (!string.IsNullOrWhiteSpace(Texto)) n++;
            if (FechaDesde.HasValue) n++;
            if (FechaHasta.HasValue) n++;
            if (!string.IsNullOrWhiteSpace(Tipo)) n++;
            return n;
        }
    }
}