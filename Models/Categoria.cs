using System.ComponentModel.DataAnnotations;

namespace Almacen.Models
{
    public class Categoria
    {
        public int Id { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public string? Descripcion { get; set; }

        public string? CodigoCgn { get; set; }

        // ✨ NUEVO — tipo de bien (devolutivo/consumo/ambos)
        public string? TipoBien { get; set; }

        public bool Estado { get; set; }

        public DateTime FechaCreacion { get; set; }

        // ✨ NUEVO — contador de bienes activos en esta categoría
        public int TotalBienes { get; set; }
    }
}