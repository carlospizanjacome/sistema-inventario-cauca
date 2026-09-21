namespace Almacen.Models;

public class Depreciacion
{
    public int Id { get; set; }
    public int BienId { get; set; }
    public int PeriodoAnio { get; set; }
    public int PeriodoMes { get; set; }
    public decimal ValorInicial { get; set; }
    public decimal ValorResidual { get; set; }
    public decimal DepreciacionMes { get; set; }
    public decimal DepreciacionAcumulada { get; set; }
    public decimal ValorNeto { get; set; }
    public DateTime CreatedAt { get; set; }
}