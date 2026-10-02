namespace Almacen.Models;

public class Entrada
{
    public int Id { get; set; }
    public int BienId { get; set; }
    public int InstitucionId { get; set; }

    public string TipoFuente { get; set; } = "FSE";
    public string? NumeroFactura { get; set; }

    public DateTime FechaEntrada { get; set; }
    public decimal Valor { get; set; }

    public string? Proveedor { get; set; }
    public int? ProveedorId { get; set; }
    public int? FuncionarioRecibeId { get; set; }

    public string? Observaciones { get; set; }

    // ═══════════════════════════════════════════════════════════
    // PROPIEDADES DE SOFT-DELETE (ANULACIÓN)
    // Preservan la trazabilidad completa del registro para auditoría.
    // ═══════════════════════════════════════════════════════════
    public bool Anulada { get; set; }
    public int? AnuladaPor { get; set; }
    public DateTime? AnuladaFecha { get; set; }
    public string? AnuladaMotivo { get; set; }
    // ═══════════════════════════════════════════════════════════

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}