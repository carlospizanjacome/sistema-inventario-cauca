using System.ComponentModel.DataAnnotations;

namespace Almacen.DTOs;

public class MovimientoConsumoDTO
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El bien es obligatorio")]
    public int BienId { get; set; }

    public int InstitucionId { get; set; }

    [Required(ErrorMessage = "El tipo es obligatorio")]
    public string TipoMovimiento { get; set; } = "ENTRADA";

    [Required(ErrorMessage = "La fecha es obligatoria")]
    public DateTime? FechaMovimiento { get; set; } = DateTime.Today;

    [Range(0.01, double.MaxValue, ErrorMessage = "La cantidad debe ser > 0")]
    public decimal Cantidad { get; set; }

    [Range(0, double.MaxValue)]
    public decimal CostoUnitario { get; set; }

    public decimal ValorMovimiento { get; set; }
    public decimal SaldoCantidad { get; set; }
    public decimal SaldoValor { get; set; }
    public decimal Cpp { get; set; }

    [StringLength(100)]
    public string? DocumentoReferencia { get; set; }

    public int? ProveedorId { get; set; }
    public int? FuncionarioRecibeId { get; set; }
    public string? Observaciones { get; set; }

    // Navegación
    public string? BienCodigo { get; set; }
    public string? BienNombre { get; set; }
    public string? UnidadMedida { get; set; }
    public string? ProveedorNombre { get; set; }
    public string? FuncionarioNombre { get; set; }
}