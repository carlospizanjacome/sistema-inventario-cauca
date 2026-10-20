using System.Text.Json;
using Almacen.Interfaces;
using Almacen.Models;
using Microsoft.AspNetCore.Http;

namespace Almacen.Services;

public class AuditoriaService
{
    private readonly IAuditoriaRepository _repo;
    private readonly UsuarioSesionService _sesion;
    private readonly IHttpContextAccessor _http;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public AuditoriaService(
        IAuditoriaRepository repo,
        UsuarioSesionService sesion,
        IHttpContextAccessor http)
    {
        _repo = repo;
        _sesion = sesion;
        _http = http;
    }

    public Task RegistrarCreacionAsync(string modulo, string objetoTipo, int? objetoId,
        string? objetoCodigo, string? objetoDescripcion, object? despues = null,
        string? motivo = null, string? docRef = null)
        => RegistrarAsync("CREAR", modulo, objetoTipo, objetoId, objetoCodigo,
            objetoDescripcion, null, despues, motivo, docRef, "INFO");

    public Task RegistrarEdicionAsync(string modulo, string objetoTipo, int? objetoId,
        string? objetoCodigo, string? objetoDescripcion, object? antes, object? despues,
        string? motivo = null, string? docRef = null)
        => RegistrarAsync("EDITAR", modulo, objetoTipo, objetoId, objetoCodigo,
            objetoDescripcion, antes, despues, motivo, docRef, "INFO");

    public Task RegistrarEliminacionAsync(string modulo, string objetoTipo, int? objetoId,
        string? objetoCodigo, string? objetoDescripcion, object? antes = null,
        string? motivo = null, string? docRef = null)
        => RegistrarAsync("ELIMINAR", modulo, objetoTipo, objetoId, objetoCodigo,
            objetoDescripcion, antes, null, motivo, docRef, "CRITICO");

    public Task RegistrarAnulacionAsync(string modulo, string objetoTipo, int? objetoId,
        string? objetoCodigo, string? objetoDescripcion,
        object? antes = null, object? despues = null,
        string? motivo = null, string? docRef = null)
        => RegistrarAsync("ANULAR", modulo, objetoTipo, objetoId, objetoCodigo,
            objetoDescripcion, antes, despues, motivo, docRef, "CRITICO");

    public Task RegistrarAprobacionAsync(string modulo, string objetoTipo, int? objetoId,
        string? objetoCodigo, string? objetoDescripcion, string? motivo = null)
        => RegistrarAsync("APROBAR", modulo, objetoTipo, objetoId, objetoCodigo,
            objetoDescripcion, null, null, motivo, null, "INFO");

    public Task RegistrarEntregaAsync(string modulo, string objetoTipo, int? objetoId,
        string? objetoCodigo, string? objetoDescripcion, string? motivo = null)
        => RegistrarAsync("ENTREGAR", modulo, objetoTipo, objetoId, objetoCodigo,
            objetoDescripcion, null, null, motivo, null, "INFO");

    public Task RegistrarDevolucionAsync(string modulo, string objetoTipo, int? objetoId,
        string? objetoCodigo, string? objetoDescripcion, string? motivo = null)
        => RegistrarAsync("DEVOLVER", modulo, objetoTipo, objetoId, objetoCodigo,
            objetoDescripcion, null, null, motivo, null, "INFO");

    public Task RegistrarCierreAsync(string modulo, string objetoTipo, int? objetoId,
        string? objetoCodigo, string? objetoDescripcion, string? motivo = null)
        => RegistrarAsync("CERRAR", modulo, objetoTipo, objetoId, objetoCodigo,
            objetoDescripcion, null, null, motivo, null, "INFO");

    public Task RegistrarExportacionAsync(string modulo, string descripcion)
        => RegistrarAsync("EXPORTAR", modulo, null, null, null, descripcion,
            null, null, null, null, "INFO");

    public Task RegistrarLoginAsync()
        => RegistrarAsync("LOGIN", "seguridad", "Usuario", _sesion.UsuarioActual?.Id,
            _sesion.UsuarioActual?.Email, _sesion.UsuarioActual?.NombreCompleto,
            null, null, null, null, "INFO");

    public Task RegistrarLogoutAsync()
        => RegistrarAsync("LOGOUT", "seguridad", "Usuario", _sesion.UsuarioActual?.Id,
            _sesion.UsuarioActual?.Email, _sesion.UsuarioActual?.NombreCompleto,
            null, null, null, null, "INFO");

    public async Task RegistrarAsync(
        string operacion,
        string modulo,
        string? objetoTipo = null,
        int? objetoId = null,
        string? objetoCodigo = null,
        string? objetoDescripcion = null,
        object? antes = null,
        object? despues = null,
        string? motivo = null,
        string? docRef = null,
        string severidad = "INFO")
    {
        try
        {
            if (_sesion.InstitucionId <= 0)
            {
                Console.WriteLine($"⚠️ AUDITORÍA: Sin institución en sesión. Evento NO registrado ({operacion} · {modulo})");
                return;
            }

            var u = _sesion.UsuarioActual;

            var reg = new Auditoria
            {
                InstitucionId = _sesion.InstitucionId,
                SedeId = _sesion.SedeId, // <--- ¡AQUÍ ESTÁ LA MAGIA!
                UsuarioId = u?.Id,
                UsuarioEmail = u?.Email,
                UsuarioNombre = u?.NombreCompleto,
                FechaHora = DateTime.Now,
                Operacion = operacion,
                Modulo = modulo,
                Severidad = severidad,
                ObjetoTipo = objetoTipo,
                ObjetoId = objetoId,
                ObjetoCodigo = Truncar(objetoCodigo, 100),
                ObjetoDescripcion = Truncar(objetoDescripcion, 300),
                ValorAntes = Serializar(antes),
                ValorDespues = Serializar(despues),
                Motivo = Truncar(motivo, 500),
                DocumentoReferencia = Truncar(docRef, 200),
                Ip = ObtenerIp(),
                UserAgent = Truncar(ObtenerUserAgent(), 300)
            };

            await _repo.InsertarAsync(reg);

            Console.WriteLine($"✅ AUDITORÍA: {operacion} · {modulo} · {objetoDescripcion}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("═══════════════════════════════════════════════════");
            Console.WriteLine($"❌ AUDITORÍA FALLÓ: {operacion} · {modulo}");
            Console.WriteLine($"   Tipo: {ex.GetType().FullName}");
            Console.WriteLine($"   Mensaje: {ex.Message}");
            Console.WriteLine($"   Inner: {ex.InnerException?.Message}");
            Console.WriteLine("═══════════════════════════════════════════════════");
        }
    }

    private static string? Serializar(object? o)
    {
        if (o is null) return null;
        try { return JsonSerializer.Serialize(o, JsonOpts); }
        catch { return null; }
    }

    private static string? Truncar(string? s, int max)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max));

    private string? ObtenerIp()
    {
        try
        {
            var ctx = _http.HttpContext;
            if (ctx is null) return null;

            var forwarded = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwarded))
                return forwarded.Split(',')[0].Trim();

            return ctx.Connection.RemoteIpAddress?.ToString();
        }
        catch { return null; }
    }

    private string? ObtenerUserAgent()
    {
        try { return _http.HttpContext?.Request.Headers.UserAgent.ToString(); }
        catch { return null; }
    }
}