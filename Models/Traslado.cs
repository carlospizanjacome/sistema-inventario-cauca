namespace Almacen.Models;

public class Traslado
{
    public int Id { get; set; }
    public int BienId { get; set; }
    public int InstitucionId { get; set; }

    public int? AulaOrigenId { get; set; }
    public int AulaDestinoId { get; set; }

    public int? FuncionarioAnteriorId { get; set; }
    public int? FuncionarioNuevoId { get; set; }

    public DateTime FechaTraslado { get; set; }
    public string? Motivo { get; set; }

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