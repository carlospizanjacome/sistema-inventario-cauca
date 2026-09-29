namespace Almacen.DTOs;

public class VidaUtilDTO
{
    public int Id { get; set; }
    public string CodigoCgn { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string? Clase { get; set; }
    public int VidaUtilMeses { get; set; }
    public decimal ValorResidualPct { get; set; }
    public bool Activo { get; set; } = true;

    /// <summary>devolutivo | consumo</summary>
    public string Tipo { get; set; } = "devolutivo";

    public string NombreCompleto =>
        $"{CodigoCgn} — {Descripcion}";

    public string NombreCompletoConVidaUtil =>
        $"{CodigoCgn} — {Descripcion} ({VidaUtilMeses} meses)";
}