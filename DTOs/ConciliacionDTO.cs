namespace Almacen.DTOs;

public class ConciliacionGlobalDTO
{
    public int TomaFisicaId { get; set; }
    public string TomaCodigo { get; set; } = string.Empty;
    public string TomaNombre { get; set; } = string.Empty;
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaCierre { get; set; }

    public int TotalSistema { get; set; }
    public decimal ValorSistema { get; set; }

    public int TotalFisico { get; set; }
    public decimal ValorFisico { get; set; }

    public int TotalConciliados { get; set; }
    public int TotalFaltantes { get; set; }
    public int TotalSobrantes { get; set; }

    public decimal ValorConciliados { get; set; }
    public decimal ValorFaltantes { get; set; }
    public decimal ValorSobrantes { get; set; }

    public int DiferenciaCantidad => TotalFisico - TotalSistema;
    public decimal DiferenciaValor => ValorFisico - ValorSistema;
    public decimal PorcentajeConciliacion => TotalSistema > 0
        ? Math.Round((decimal)TotalConciliados / TotalSistema * 100, 2)
        : 0;
}

public class ConciliacionCategoriaDTO
{
    public int? CategoriaId { get; set; }
    public string CategoriaNombre { get; set; } = "Sin categoría";
    public string? CodigoCgn { get; set; }

    public int CantidadSistema { get; set; }
    public int CantidadFisica { get; set; }
    public decimal ValorSistema { get; set; }
    public decimal ValorFisico { get; set; }
    public int Conciliados { get; set; }
    public int Faltantes { get; set; }
    public int Sobrantes { get; set; }

    public int DiferenciaCantidad => CantidadFisica - CantidadSistema;
    public decimal DiferenciaValor => ValorFisico - ValorSistema;
    public bool TieneDiferencias => DiferenciaCantidad != 0 || DiferenciaValor != 0;
}

public class ConciliacionDetalleDTO
{
    public int DetalleId { get; set; }
    public int? BienId { get; set; }
    public string CodigoSnapshot { get; set; } = string.Empty;
    public string NombreSnapshot { get; set; } = string.Empty;
    public string? CategoriaNombre { get; set; }
    public string? CodigoCgn { get; set; }

    public string Tipo { get; set; } = string.Empty;
    public bool Encontrado { get; set; }

    public decimal? ValorAdquisicion { get; set; }
    public string? UbicacionSnapshot { get; set; }
    public string? UbicacionReal { get; set; }
    public string? FuncionarioSnapshot { get; set; }
    public string? FuncionarioReal { get; set; }
    public string? EstadoFisicoReal { get; set; }
    public DateTime? FechaEscaneo { get; set; }
    public string? Observaciones { get; set; }

    public bool EsFaltante => Tipo == "FALTANTE" || (!Encontrado && Tipo == "PENDIENTE");
    public bool EsSobrante => Tipo == "SOBRANTE";
    public bool UbicacionDiferente =>
        !string.IsNullOrWhiteSpace(UbicacionSnapshot) &&
        !string.IsNullOrWhiteSpace(UbicacionReal) &&
        !string.Equals(UbicacionSnapshot?.Trim(), UbicacionReal?.Trim(), StringComparison.OrdinalIgnoreCase);
    public bool FuncionarioDiferente =>
        !string.IsNullOrWhiteSpace(FuncionarioSnapshot) &&
        !string.IsNullOrWhiteSpace(FuncionarioReal) &&
        !string.Equals(FuncionarioSnapshot?.Trim(), FuncionarioReal?.Trim(), StringComparison.OrdinalIgnoreCase);
}

public class TomaFisicaCerradaDTO
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaCierre { get; set; }
    public int TotalBienes { get; set; }
    public string Etiqueta => $"{Codigo} — {Nombre} ({FechaInicio:dd/MM/yyyy})";
}