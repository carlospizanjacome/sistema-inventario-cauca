namespace Almacen.Models
{
    public class Bien
    {
        public int Id { get; set; }

        public string Codigo { get; set; } = string.Empty;

        public string Nombre { get; set; } = string.Empty;

        public string? Descripcion { get; set; }

        public int? CategoriaId { get; set; }

        public string TipoBien { get; set; } = "devolutivo";

        public string? Marca { get; set; }

        public string? Modelo { get; set; }

        public string? Serie { get; set; }

        public decimal ValorAdquisicion { get; set; }

        public DateTime? FechaAdquisicion { get; set; }

        public string EstadoFisico { get; set; } = "bueno";

        public string? Ubicacion { get; set; }

        public string? Responsable { get; set; }

        public bool Activo { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public int? InstitucionId { get; set; }
        public int? AulaId { get; set; }
        public int? FuncionarioId { get; set; }
        public int? VidaUtilId { get; set; }
        public string? CodigoQr { get; set; }
        public decimal ValorResidual { get; set; }

        public int Cantidad { get; set; } = 1;
        public decimal DepreciacionAcumulada { get; set; }
        public decimal ValorNeto { get; set; }

        // ── Navegación (read-only, vienen de los JOINs) ──
        public string? CategoriaNombre { get; set; }
        public string? AulaNombre { get; set; }
        public string? BloqueNombre { get; set; }
        public string? SedeNombre { get; set; }
        public string? FuncionarioNombre { get; set; }
    }
}