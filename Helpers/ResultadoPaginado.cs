namespace Almacen.Helpers;

/// <summary>
/// Envoltorio genérico para resultados paginados.
/// Contiene los items de la página actual y metadata de navegación.
/// </summary>
public class ResultadoPaginado<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalRegistros { get; set; }
    public int PaginaActual { get; set; }
    public int TamanoPagina { get; set; }

    public int TotalPaginas => TamanoPagina > 0
        ? (int)Math.Ceiling((double)TotalRegistros / TamanoPagina)
        : 0;

    public bool TieneAnterior => PaginaActual > 1;
    public bool TieneSiguiente => PaginaActual < TotalPaginas;

    public int PrimerRegistro => TotalRegistros == 0
        ? 0
        : (PaginaActual - 1) * TamanoPagina + 1;

    public int UltimoRegistro => Math.Min(PaginaActual * TamanoPagina, TotalRegistros);
}