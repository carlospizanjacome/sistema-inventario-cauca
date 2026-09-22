namespace Almacen.Models;

public class TomaFisica
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int InstitucionId { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaCierre { get; set; }
    public string Estado { get; set; } = "BORRADOR";
    public int? ResponsableId { get; set; }

    public int TotalBienesSnapshot { get; set; }
    public int TotalEncontrados { get; set; }
    public int TotalFaltantes { get; set; }
    public int TotalSobrantes { get; set; }

    public string? Observaciones { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}

public class TomaFisicaDetalle
{
    public int Id { get; set; }
    public int TomaFisicaId { get; set; }

    // Snapshot
    public int? BienId { get; set; }
    public string CodigoSnapshot { get; set; } = string.Empty;
    public string NombreSnapshot { get; set; } = string.Empty;
    public string? UbicacionSnapshot { get; set; }
    public string? FuncionarioSnapshot { get; set; }

    // Validación
    public string Tipo { get; set; } = "PENDIENTE";
    public bool Encontrado { get; set; }
    public string? UbicacionReal { get; set; }
    public int? FuncionarioRealId { get; set; }
    public string? EstadoFisicoReal { get; set; }

    // Auditoría
    public int? EscaneadoPor { get; set; }
    public DateTime? FechaEscaneo { get; set; }
    public string? Observaciones { get; set; }
    public DateTime CreatedAt { get; set; }
}