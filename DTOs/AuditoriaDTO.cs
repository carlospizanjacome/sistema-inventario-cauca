namespace Almacen.DTOs;

public class AuditoriaFiltro
{
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public string? Modulo { get; set; }
    public string? Operacion { get; set; }
    public string? Severidad { get; set; }
    public int? UsuarioId { get; set; }
    public string? Busqueda { get; set; }
    public int Pagina { get; set; } = 1;
    public int Tamano { get; set; } = 25;
}

public class AuditoriaItemDTO
{
    public long Id { get; set; }
    public DateTime FechaHora { get; set; }
    public string? UsuarioNombre { get; set; }
    public string? UsuarioEmail { get; set; }
    public string Operacion { get; set; } = "";
    public string Modulo { get; set; } = "";
    public string Severidad { get; set; } = "INFO";
    public string? ObjetoTipo { get; set; }
    public int? ObjetoId { get; set; }
    public string? ObjetoCodigo { get; set; }
    public string? ObjetoDescripcion { get; set; }
    public string? Motivo { get; set; }
    public string? DocumentoReferencia { get; set; }
    public string? ValorAntes { get; set; }
    public string? ValorDespues { get; set; }
    public string? Ip { get; set; }
}

public class AuditoriaPaginaDTO
{
    public List<AuditoriaItemDTO> Items { get; set; } = new();
    public int TotalRegistros { get; set; }
    public int PaginaActual { get; set; }
    public int Tamano { get; set; }
    public int TotalPaginas => Tamano <= 0 ? 0 : (int)Math.Ceiling((double)TotalRegistros / Tamano);
}

public class AuditoriaResumenDTO
{
    public int EventosHoy { get; set; }
    public int EventosSemana { get; set; }
    public int EventosMes { get; set; }
    public int EventosCriticos { get; set; }
    public int TotalRegistros { get; set; }
}

public class AuditoriaUsuarioOpcionDTO
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Email { get; set; } = "";
}