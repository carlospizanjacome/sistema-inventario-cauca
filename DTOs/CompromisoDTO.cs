namespace Almacen.DTOs;

/// <summary>
/// Resultado de la verificación de "bien comprometido".
/// Si tiene registros en otras tablas → no se puede eliminar.
/// </summary>
public class CompromisoDTO
{
    public List<DetalleCompromiso> Detalles { get; set; } = new();

    public bool Comprometido => Detalles.Any();

    /// <summary>
    /// Texto formateado para mostrar en el modal de advertencia.
    /// </summary>
    public string MensajeFormateado => string.Join("\n", Detalles.Select(d =>
    {
        var plural = d.Cantidad > 1 ? "s" : "";
        var refTexto = string.IsNullOrWhiteSpace(d.Referencia) ? "" : $"  ({d.Referencia})";
        return $"• {d.Cantidad} {d.Tipo.ToLower()}{plural}{refTexto}";
    }));
}

public class DetalleCompromiso
{
    public string Tipo { get; set; } = "";
    public string Icono { get; set; } = "bi-info-circle";
    public int Cantidad { get; set; }
    public string? Referencia { get; set; }
}