using System.ComponentModel.DataAnnotations;

namespace Almacen.DTOs;

public class MovimientoConsumoDTO
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El bien es obligatorio")]
    public int BienId { get; set; }

    public int InstitucionId { get; set; }

    [Required(ErrorMessage = "El tipo de movimiento es obligatorio")]
    [StringLength(20, ErrorMessage = "El tipo no puede superar 20 caracteres")]
    public string TipoMovimiento { get; set; } = "ENTRADA";

    [Required(ErrorMessage = "La fecha es obligatoria")]
    public DateTime? FechaMovimiento { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "La cantidad es obligatoria")]
    [Range(0.01, 999999999.99, ErrorMessage = "La cantidad debe ser mayor a 0")]
    public decimal Cantidad { get; set; }

    [Range(0, 999999999.99, ErrorMessage = "El costo no puede ser negativo")]
    public decimal CostoUnitario { get; set; }

    public decimal ValorMovimiento { get; set; }
    public decimal SaldoCantidad { get; set; }
    public decimal SaldoValor { get; set; }
    public decimal Cpp { get; set; }

    [StringLength(100, ErrorMessage = "El documento no puede superar 100 caracteres")]
    public string? DocumentoReferencia { get; set; }

    public int? ProveedorId { get; set; }
    public int? FuncionarioRecibeId { get; set; }

    [StringLength(500, ErrorMessage = "Las observaciones no pueden superar 500 caracteres")]
    public string? Observaciones { get; set; }

    // ═══ Navegación (solo lectura) ═══
    public string? BienCodigo { get; set; }
    public string? BienNombre { get; set; }
    public string? UnidadMedida { get; set; }
    public string? ProveedorNombre { get; set; }
    public string? FuncionarioNombre { get; set; }
}