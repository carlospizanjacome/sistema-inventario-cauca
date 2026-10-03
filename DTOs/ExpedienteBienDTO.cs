namespace Almacen.DTOs;

// ═══════════════════════════════════════════════════════════
// EXPEDIENTE DEL BIEN — Contenedor principal
// Vista 360° del ciclo de vida (V2.0 §10)
// ═══════════════════════════════════════════════════════════
public class ExpedienteBienDTO
{
    // ═══ Identificación ═══
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public string? CodigoQr { get; set; }
    public int Cantidad { get; set; } = 1;

    // ═══ Clasificación ═══
    public string TipoBien { get; set; } = "devolutivo";
    public string? CategoriaNombre { get; set; }
    public string? CodigoCgn { get; set; }
    public int? VidaUtilMeses { get; set; }
    public int AniosVidaUtil => VidaUtilMeses.HasValue
        ? (int)Math.Round(VidaUtilMeses.Value / 12.0, MidpointRounding.AwayFromZero)
        : 0;

    // ═══ Características ═══
    public string? Marca { get; set; }
    public string? Modelo { get; set; }
    public string? Serie { get; set; }

    // ═══ Valoración ═══
    public decimal ValorAdquisicion { get; set; }
    public decimal ValorResidual { get; set; }
    public decimal DepreciacionAcumulada { get; set; }
    public decimal ValorNeto { get; set; }
    public DateTime? FechaAdquisicion { get; set; }
    public DateTime? FechaUltimoCalculo { get; set; }

    public decimal PorcentajeDepreciado => ValorAdquisicion > 0
        ? Math.Round(DepreciacionAcumulada / ValorAdquisicion * 100, 2)
        : 0;

    // ═══ Estado ═══
    public string EstadoFisico { get; set; } = "bueno";
    public bool Activo { get; set; }

    // ═══ Ubicación ═══
    public string? InstitucionNombre { get; set; }
    public string? SedeNombre { get; set; }
    public string? BloqueNombre { get; set; }
    public string? AulaNombre { get; set; }
    public string? Ubicacion { get; set; }  // Legacy

    // ═══ Responsable ═══
    public string? FuncionarioNombre { get; set; }
    public string? FuncionarioCedula { get; set; }
    public string? FuncionarioCargo { get; set; }
    public string? Responsable { get; set; }  // Legacy

    // ═══ Consumo (si aplica) ═══
    public string? UnidadMedida { get; set; }
    public decimal? StockActual { get; set; }
    public decimal? StockMinimo { get; set; }
    public decimal? CppActual { get; set; }
    public decimal? ValorStock { get; set; }

    // ═══ Timestamps ═══
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // ═══════════════════════════════════════════════════════
    // HISTORIAL — Para los tabs del Expediente
    // ═══════════════════════════════════════════════════════

    // TAB: Movimientos
    public List<ExpedienteEntradaDTO> Entradas { get; set; } = new();
    public List<ExpedienteTrasladoDTO> Traslados { get; set; } = new();
    public List<ExpedienteSalidaDTO> Salidas { get; set; } = new();

    public int TotalMovimientos =>
        Entradas.Count + Traslados.Count + Salidas.Count;

    // TAB: Depreciación
    public List<ExpedienteDepreciacionDTO> Depreciaciones { get; set; } = new();

    // TAB: Tomas físicas
    public List<ExpedienteTomaFisicaDTO> TomasFisicas { get; set; } = new();
}

// ═══════════════════════════════════════════════════════════
// MOVIMIENTOS — Entrada
// ═══════════════════════════════════════════════════════════
public class ExpedienteEntradaDTO
{
    public int Id { get; set; }
    public string TipoFuente { get; set; } = "";
    public DateTime? FechaEntrada { get; set; }
    public string? NumeroFactura { get; set; }
    public string? ProveedorNombre { get; set; }
    public string? FuncionarioNombre { get; set; }
    public decimal Valor { get; set; }
    public string? Observaciones { get; set; }
}

// ═══════════════════════════════════════════════════════════
// MOVIMIENTOS — Traslado
// ═══════════════════════════════════════════════════════════
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

// ═══════════════════════════════════════════════════════════
// MOVIMIENTOS — Salida / Baja
// ═══════════════════════════════════════════════════════════
public class ExpedienteSalidaDTO
{
    public int Id { get; set; }
    public string TipoBaja { get; set; } = "";
    public DateTime? FechaSalida { get; set; }
    public string? NumeroActaComite { get; set; }
    public string? NumeroDenuncia { get; set; }
    public string? FuncionarioApruebaNombre { get; set; }
    public decimal ValorSalida { get; set; }
    public string? Motivo { get; set; }
}

// ═══════════════════════════════════════════════════════════
// DEPRECIACIÓN (histórico mensual)
// ═══════════════════════════════════════════════════════════
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
    public DateTime CreatedAt { get; set; }
}

// ═══════════════════════════════════════════════════════════
// TOMA FÍSICA (participación en cada toma)
// ═══════════════════════════════════════════════════════════
public class ExpedienteTomaFisicaDTO
{
    public int Id { get; set; }
    public int TomaId { get; set; }
    public string TomaCodigo { get; set; } = "";
    public string TomaNombre { get; set; } = "";
    public string Tipo { get; set; } = "PENDIENTE";
    public bool Encontrado { get; set; }
    public string? UbicacionSnapshot { get; set; }
    public string? UbicacionReal { get; set; }
    public string? FuncionarioSnapshot { get; set; }
    public string? FuncionarioRealNombre { get; set; }
    public string? Observaciones { get; set; }
    public DateTime? FechaEscaneo { get; set; }
}