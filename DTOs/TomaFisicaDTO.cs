using System.ComponentModel.DataAnnotations;

namespace Almacen.DTOs;

public class TomaFisicaDTO
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El código es obligatorio")]
    [StringLength(30)]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(200)]
    public string Nombre { get; set; } = string.Empty;

    public int InstitucionId { get; set; }

    [Required(ErrorMessage = "La fecha de inicio es obligatoria")]
    public DateTime? FechaInicio { get; set; } = DateTime.Today;

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

    // Navegación
    public string? InstitucionNombre { get; set; }
    public string? ResponsableNombre { get; set; }

    // Calculado
    public int TotalPendientes => TotalBienesSnapshot - TotalEncontrados;
    public decimal PorcentajeAvance => TotalBienesSnapshot > 0
        ? Math.Round((decimal)TotalEncontrados / TotalBienesSnapshot * 100, 2)
        : 0;
}

public class TomaFisicaDetalleDTO
{
    public int Id { get; set; }
    public int TomaFisicaId { get; set; }

    public int? BienId { get; set; }
    public string CodigoSnapshot { get; set; } = string.Empty;
    public string NombreSnapshot { get; set; } = string.Empty;
    public string? UbicacionSnapshot { get; set; }
    public string? FuncionarioSnapshot { get; set; }

    public string Tipo { get; set; } = "PENDIENTE";
    public bool Encontrado { get; set; }
    public string? UbicacionReal { get; set; }
    public int? FuncionarioRealId { get; set; }
    public string? EstadoFisicoReal { get; set; }

    public int? EscaneadoPor { get; set; }
    public DateTime? FechaEscaneo { get; set; }
    public string? Observaciones { get; set; }

    // Navegación
    public string? FuncionarioRealNombre { get; set; }
    public string? UsuarioEscaneoNombre { get; set; }
}

/// <summary>
/// DTO para el escaneo desde la app móvil.
/// </summary>
public class EscaneoDTO
{
    [Required]
    public string CodigoQr { get; set; } = string.Empty;
    public int? FuncionarioRealId { get; set; }
    public string? UbicacionReal { get; set; }
    public string? EstadoFisicoReal { get; set; }
    public string? Observaciones { get; set; }
}

/// <summary>
/// DTO resumen para el reporte final (Conciliados/Sobrantes/Faltantes).
/// </summary>
public class ReporteTomaFisicaDTO
{
    public int TomaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string InstitucionNombre { get; set; } = string.Empty;
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaCierre { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string? ResponsableNombre { get; set; }

    public int TotalBienesSnapshot { get; set; }
    public int TotalConciliados { get; set; }
    public int TotalFaltantes { get; set; }
    public int TotalSobrantes { get; set; }
    public int TotalPendientes { get; set; }

    public decimal PorcentajeConciliacion { get; set; }

    public List<TomaFisicaDetalleDTO> DetallesConciliados { get; set; } = new();
    public List<TomaFisicaDetalleDTO> DetallesFaltantes { get; set; } = new();
    public List<TomaFisicaDetalleDTO> DetallesSobrantes { get; set; } = new();
}