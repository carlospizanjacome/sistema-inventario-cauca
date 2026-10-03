namespace Almacen.DTOs;

// ═══════════════════════════════════════════════════════════
// IMPORTACIÓN — Cabecera
// ═══════════════════════════════════════════════════════════
public class ImportacionDTO
{
    public int Id { get; set; }
    public int InstitucionId { get; set; }
    public int UsuarioId { get; set; }
    public string NombreArchivo { get; set; } = "";
    public string TipoArchivo { get; set; } = "";

    public int TotalFilas { get; set; }
    public int FilasExitosas { get; set; }
    public int FilasConWarning { get; set; }
    public int FilasConError { get; set; }
    public int FilasOmitidas { get; set; }

    public string Estado { get; set; } = "EN_PROCESO";
    public string? MapeoColumnas { get; set; }
    public string? Observaciones { get; set; }

    public DateTime FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }

    // Navegación
    public string? UsuarioNombre { get; set; }
    public string? InstitucionNombre { get; set; }

    // Calculado
    public int FilasProcesadas => FilasExitosas + FilasConWarning + FilasConError + FilasOmitidas;
    public decimal PorcentajeExito => TotalFilas > 0
        ? Math.Round((decimal)FilasExitosas / TotalFilas * 100, 2) : 0;
}

// ═══════════════════════════════════════════════════════════
// IMPORTACIÓN — Detalle por fila
// ═══════════════════════════════════════════════════════════
public class ImportacionDetalleDTO
{
    public int Id { get; set; }
    public int ImportacionId { get; set; }
    public int NumeroFila { get; set; }
    public string DatosCrudos { get; set; } = "";
    public string Estado { get; set; } = "PENDIENTE";
    public string? Errores { get; set; }
    public string? Advertencias { get; set; }
    public int? BienId { get; set; }

    // Navegación
    public string? BienCodigo { get; set; }
    public string? BienNombre { get; set; }
}

// ═══════════════════════════════════════════════════════════
// PARSEO DE EXCEL — Preview
// ═══════════════════════════════════════════════════════════
public class PreviewExcelDTO
{
    public List<string> Hojas { get; set; } = new();
    public string HojaSeleccionada { get; set; } = "";
    public List<string> Columnas { get; set; } = new();
    public List<Dictionary<string, string>> PrimerasFilas { get; set; } = new();
    public int TotalFilasEstimadas { get; set; }
}

// ═══════════════════════════════════════════════════════════
// MAPEO DE COLUMNAS
// ═══════════════════════════════════════════════════════════
public class MapeoColumnasDTO
{
    // Key: campo destino en DB
    // Value: nombre de la columna en el Excel
    public Dictionary<string, string> Mapeo { get; set; } = new();
}

// ═══════════════════════════════════════════════════════════
// FILA PROCESADA — Para validación
// ═══════════════════════════════════════════════════════════
public class FilaImportacionDTO
{
    public int NumeroFila { get; set; }
    public Dictionary<string, string> DatosCrudos { get; set; } = new();

    // Datos parseados
    public string? Nombre { get; set; }
    public string? Codigo { get; set; }
    public string? CategoriaNombre { get; set; }
    public int? CategoriaId { get; set; }
    public string? TipoBien { get; set; }
    public decimal? ValorAdquisicion { get; set; }
    public DateTime? FechaAdquisicion { get; set; }
    public string? Marca { get; set; }
    public string? Modelo { get; set; }
    public string? Serie { get; set; }
    public string? EstadoFisico { get; set; }
    public string? AulaNombre { get; set; }
    public int? AulaId { get; set; }
    public string? FuncionarioNombre { get; set; }
    public int? FuncionarioId { get; set; }
    public string? Observaciones { get; set; }
    public string? UnidadMedida { get; set; }
    public decimal? StockInicial { get; set; }
    public decimal? StockMinimo { get; set; }
    public string? FuenteFinanciacion { get; set; }

    // Estado de validación
    public string Estado { get; set; } = "PENDIENTE";
    public List<string> Errores { get; set; } = new();
    public List<string> Advertencias { get; set; } = new();
}

// ═══════════════════════════════════════════════════════════
// RESULTADO DE IMPORTACIÓN
// ═══════════════════════════════════════════════════════════
public class ResultadoImportacionDTO
{
    public int ImportacionId { get; set; }
    public bool Exito { get; set; }
    public string Mensaje { get; set; } = "";
    public int TotalFilas { get; set; }
    public int FilasImportadas { get; set; }
    public int FilasConWarning { get; set; }
    public int FilasConError { get; set; }
    public int FilasDuplicadas { get; set; }
    public List<string> ErroresGlobales { get; set; } = new();
}

// ═══════════════════════════════════════════════════════════
// PLANTILLA DE MAPEO GUARDADA
// ═══════════════════════════════════════════════════════════
public class PlantillaMapeoDTO
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string MapeoJson { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}