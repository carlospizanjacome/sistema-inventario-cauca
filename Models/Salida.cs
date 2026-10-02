namespace Almacen.Models;

public class Salida
{
    public int Id { get; set; }
    public int BienId { get; set; }
    public int InstitucionId { get; set; }

    public string TipoBaja { get; set; } = "Obsolescencia";
    public string Motivo { get; set; } = "";

    public DateTime FechaSalida { get; set; }

    public string? NumeroActaComite { get; set; }
    public string? NumeroDenuncia { get; set; }

    public decimal ValorSalida { get; set; }
    public int? FuncionarioApruebaId { get; set; }

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