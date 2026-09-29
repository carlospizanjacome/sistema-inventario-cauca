namespace Almacen.Models;

public class Prestamo
{
    public int Id { get; set; }
    public int BienId { get; set; }
    public int InstitucionId { get; set; }
    public int FuncionarioSolicitaId { get; set; }
    public int? FuncionarioApruebaId { get; set; }

    public DateTime FechaSolicitud { get; set; }
    public DateTime? FechaAprobacion { get; set; }
    public DateTime? FechaPrestamo { get; set; }
    public DateTime FechaDevolucionPrevista { get; set; }
    public DateTime? FechaDevolucionReal { get; set; }

    public string Estado { get; set; } = "SOLICITADO";

    public string Motivo { get; set; } = string.Empty;
    public string? ObservacionesEntrega { get; set; }
    public string? ObservacionesDevolucion { get; set; }
    public string? EstadoBienEntrega { get; set; }
    public string? EstadoBienDevolucion { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}