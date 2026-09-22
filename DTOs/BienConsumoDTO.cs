using System.ComponentModel.DataAnnotations;

namespace Almacen.DTOs;

public class BienConsumoDTO
{
    public int Id { get; set; }
    public int InstitucionId { get; set; }

    [Required(ErrorMessage = "El código es obligatorio")]
    [StringLength(30)]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(150)]
    public string Nombre { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    public int? CategoriaId { get; set; }

    [Required(ErrorMessage = "La unidad de medida es obligatoria")]
    [StringLength(20)]
    public string UnidadMedida { get; set; } = "Unidad";

    [Range(0, double.MaxValue, ErrorMessage = "El stock mínimo debe ser >= 0")]
    public decimal StockMinimo { get; set; }

    public decimal StockActual { get; set; }
    public decimal CppActual { get; set; }
    public decimal ValorStock { get; set; }

    public bool Activo { get; set; } = true;

    // Navegación
    public string? CategoriaNombre { get; set; }

    // Calculado
    public bool BajoStockMinimo => StockActual <= StockMinimo;
}