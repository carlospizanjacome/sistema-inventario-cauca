namespace Almacen.DTOs;

/// <summary>
/// Vista 360° de un bien — consolida datos, movimientos, depreciación y tomas físicas.
/// Documento Maestro V2.0 — Sección 10 (Expediente del Bien)
/// </summary>
public class ExpedienteBienDTO
{
    // ═══ Identidad ═══
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string TipoBien { get; set; } = "devolutivo";
    public string EstadoFisico { get; set; } = "bueno";
    public bool Activo { get; set; }
    public string? CodigoQr { get; set; }

    public string? Marca { get; set; }
    public string? Modelo { get; set; }
    public string? Serie { get; set; }

    // ═══ Clasificación ═══
    public string? CategoriaNombre { get; set; }
    public string? CodigoCgn { get; set; }
    public string? VidaUtilDescripcion { get; set; }
    public int? VidaUtilMeses { get; set; }

    // ═══ Ubicación y Responsable ═══
    public string? AulaNombre { get; set; }
    public string? BloqueNombre { get; set; }
    public string? SedeNombre { get; set; }
    public string? InstitucionNombre { get; set; }
    public string? FuncionarioNombre { get; set; }
    public string? FuncionarioCedula { get; set; }
    public string? FuncionarioCargo { get; set; }

    // ═══ Valoración ═══
    public decimal ValorAdquisicion { get; set; }
    public decimal ValorResidual { get; set; }
    public decimal DepreciacionAcumulada { get; set; }
    public decimal ValorNeto { get; set; }
    public DateTime? FechaAdquisicion { get; set; }
    public DateTime? FechaUltimoCalculo { get; set; }

    public int Cantidad { get; set; } = 1;

    // ═══ Fechas sistema ═══
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // ═══ Movimientos ═══
    public List<ExpedienteEntradaDTO> Entradas { get; set; } = new();
    public List<ExpedienteSalidaDTO> Salidas { get; set; } = new();
    public List<ExpedienteTrasladoDTO> Traslados { get; set; } = new();
    public List<ExpedienteDepreciacionDTO> Depreciaciones { get; set; } = new();
    public List<ExpedienteTomaFisicaDTO> TomasFisicas { get; set; } = new();

    // ═══ Métricas calculadas ═══
    public int TotalMovimientos => Entradas.Count + Salidas.Count + Traslados.Count;
    public int AniosVidaUtil => (VidaUtilMeses ?? 0) / 12;
    public decimal PorcentajeDepreciado
    {
        get
        {
            var baseDep = ValorAdquisicion - ValorResidual;
            if (baseDep <= 0) return 0;
            return Math.Round(DepreciacionAcumulada / baseDep * 100, 2);
        }
    }
}

// ═══════════════════════════════════════════════════════════
// DTOs de movimientos (livianos, sin DataAnnotations)
// ═══════════════════════════════════════════════════════════

public class ExpedienteEntradaDTO
{
    public int Id { get; set; }
    public string TipoFuente { get; set; } = string.Empty;
    public string? NumeroFactura { get; set; }
    public DateTime? FechaEntrada { get; set; }
    public decimal Valor { get; set; }
    public string? ProveedorNombre { get; set; }
    public string? FuncionarioNombre { get; set; }
    public string? Observaciones { get; set; }
}

public class ExpedienteSalidaDTO
{
    public int Id { get; set; }
    public string TipoBaja { get; set; } = string.Empty;
    public string? Motivo { get; set; }
    public DateTime? FechaSalida { get; set; }
    public string? NumeroActaComite { get; set; }
    public string? NumeroDenuncia { get; set; }
    public decimal ValorSalida { get; set; }
    public string? FuncionarioApruebaNombre { get; set; }
    public string? Observaciones { get; set; }
}

public class ExpedienteTrasladoDTO
{
    public int Id { get; set; }
    public DateTime? FechaTraslado { get; set; }
    public string? AulaOrigenNombre { get; set; }
    public string? AulaDestinoNombre { get; set; }
    public string? FuncionarioAnteriorNombre { get; set; }
    public string? FuncionarioNuevoNombre { get; set; }
    public string? Motivo { get; set; }
}

public class ExpedienteDepreciacionDTO
{
    public int Id { get; set; }
    public int PeriodoAnio { get; set; }
    public int PeriodoMes { get; set; }
    public decimal ValorInicial { get; set; }
    public decimal ValorResidual { get; set; }
    public decimal DepreciacionMes { get; set; }
    public decimal DepreciacionAcumulada { get; set; }
    public decimal ValorNeto { get; set; }
}

public class ExpedienteTomaFisicaDTO
{
    public int Id { get; set; }
    public int TomaFisicaId { get; set; }
    public string? TomaCodigo { get; set; }
    public string? TomaNombre { get; set; }
    public string? CodigoSnapshot { get; set; }
    public string? UbicacionReal { get; set; }
    public string? FuncionarioRealNombre { get; set; }
    public string? EstadoFisicoReal { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public bool Encontrado { get; set; }
    public DateTime? FechaEscaneo { get; set; }
    public string? Observaciones { get; set; }
}