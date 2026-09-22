namespace Almacen.DTOs;

/// <summary>
/// Filtros avanzados para búsqueda de bienes.
/// Todos los campos son opcionales (null = sin filtro).
/// </summary>
public class FiltroBienDTO
{
    public string? Texto { get; set; }
    public int? CategoriaId { get; set; }
    public int? SedeId { get; set; }
    public int? AulaId { get; set; }
    public int? FuncionarioId { get; set; }
    public string? EstadoFisico { get; set; }
    public string? TipoBien { get; set; }
    public bool? Activo { get; set; }
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }

    public bool HayFiltros =>
        !string.IsNullOrWhiteSpace(Texto) ||
        CategoriaId.HasValue ||
        SedeId.HasValue ||
        AulaId.HasValue ||
        FuncionarioId.HasValue ||
        !string.IsNullOrWhiteSpace(EstadoFisico) ||
        !string.IsNullOrWhiteSpace(TipoBien) ||
        Activo.HasValue ||
        FechaDesde.HasValue ||
        FechaHasta.HasValue;

    public int CantidadFiltros
    {
        get
        {
            var n = 0;
            if (!string.IsNullOrWhiteSpace(Texto)) n++;
            if (CategoriaId.HasValue) n++;
            if (SedeId.HasValue) n++;
            if (AulaId.HasValue) n++;
            if (FuncionarioId.HasValue) n++;
            if (!string.IsNullOrWhiteSpace(EstadoFisico)) n++;
            if (!string.IsNullOrWhiteSpace(TipoBien)) n++;
            if (Activo.HasValue) n++;
            if (FechaDesde.HasValue) n++;
            if (FechaHasta.HasValue) n++;
            return n;
        }
    }
}