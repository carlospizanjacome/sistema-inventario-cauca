namespace Almacen.Models;

public class MovimientoConsumo
{
    public int Id { get; set; }
    public int BienId { get; set; }
    public int InstitucionId { get; set; }
    public string TipoMovimiento { get; set; } = string.Empty;
    // ENTRADA, SALIDA, AJUSTE_POS, AJUSTE_NEG
    public DateTime FechaMovimiento { get; set; }
    public decimal Cantidad { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal ValorMovimiento { get; set; }
    public decimal SaldoCantidad { get; set; }
    public decimal SaldoValor { get; set; }
    public decimal Cpp { get; set; }
    public string? DocumentoReferencia { get; set; }
    public int? ProveedorId { get; set; }
    public int? FuncionarioRecibeId { get; set; }
    public string? Observaciones { get; set; }
    public DateTime CreatedAt { get; set; }
}