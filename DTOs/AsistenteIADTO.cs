namespace Almacen.DTOs;

// ═══════════════════════════════════════════════════════════════
// DTOs del Asistente IA
// ═══════════════════════════════════════════════════════════════

public class AsistenteResumenDTO
{
    public int TotalDevolutivos { get; set; }
    public int TotalConsumibles { get; set; }
    public int TotalBajas { get; set; }
    public decimal ValorTotalInventario { get; set; }
    public decimal DepreciacionTotal { get; set; }
    public decimal ValorNetoTotal { get; set; }
    public int SinResponsable { get; set; }
    public int SinUbicacion { get; set; }
}

/// <summary>
/// Respuesta del asistente ante una pregunta del usuario.
/// </summary>
public class RespuestaAsistenteDTO
{
    public string Pregunta { get; set; } = "";
    public string Respuesta { get; set; } = "";
    public string TipoRespuesta { get; set; } = "info"; // info, warning, danger, success, table
    public List<RespuestaTablaDTO>? Tabla { get; set; }
    public bool Exito { get; set; } = true;
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string? Sugerencia { get; set; }
}

public class RespuestaTablaDTO
{
    public List<string> Columnas { get; set; } = new();
    public List<List<string>> Filas { get; set; } = new();
}

/// <summary>
/// Tarjeta de pregunta frecuente para la UI.
/// </summary>
public class PreguntaFrecuenteDTO
{
    public string Id { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string Icono { get; set; } = "";
    public string Color { get; set; } = "blue";
    public string Categoria { get; set; } = "";
}

/// <summary>
/// Mensaje individual en el chat web.
/// </summary>
public class MensajeChatDTO
{
    public string Rol { get; set; } = "user"; // user | bot
    public string Contenido { get; set; } = "";
    public string TipoRespuesta { get; set; } = "info";
    public List<RespuestaTablaDTO>? Tabla { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
}