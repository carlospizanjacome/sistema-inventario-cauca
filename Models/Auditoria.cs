namespace Almacen.Models;

public class Auditoria
{
    public long Id { get; set; }
    public int InstitucionId { get; set; }

    public int? UsuarioId { get; set; }
    public string? UsuarioEmail { get; set; }
    public string? UsuarioNombre { get; set; }

    public DateTime FechaHora { get; set; }

    public string Operacion { get; set; } = "";
    public string Modulo { get; set; } = "";
    public string Severidad { get; set; } = "INFO";

    public string? ObjetoTipo { get; set; }
    public int? ObjetoId { get; set; }
    public string? ObjetoCodigo { get; set; }
    public string? ObjetoDescripcion { get; set; }

    public string? ValorAntes { get; set; }
    public string? ValorDespues { get; set; }

    public string? Motivo { get; set; }
    public string? DocumentoReferencia { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
}