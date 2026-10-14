using Dapper;
using Almacen.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Almacen.Services;

/// <summary>
/// Motor de respuestas del Asistente IA.
/// Resuelve preguntas frecuentes consultando vistas materializadas.
/// Este servicio lo usan: la Boca Web (Blazor) Y la Boca WhatsApp (WAJA).
/// </summary>
public class AsistenteIAService
{
    private readonly string _connectionString;
    private readonly ILogger<AsistenteIAService> _logger;

    public AsistenteIAService(IConfiguration configuration, ILogger<AsistenteIAService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Cadena de conexión 'DefaultConnection' no configurada.");
        _logger = logger;
    }

    private async Task<NpgsqlConnection> CrearConexionAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    // ═══════════════════════════════════════════════════════════
    // CATÁLOGO DE PREGUNTAS FRECUENTES
    // ═══════════════════════════════════════════════════════════
    public static List<PreguntaFrecuenteDTO> ObtenerPreguntasFrecuentes()
    {
        return new List<PreguntaFrecuenteDTO>
        {
            new() { Id = "resumen",       Titulo = "Dame un resumen del inventario",    Icono = "bi-speedometer2",       Color = "blue",   Categoria = "General" },
            new() { Id = "stock_critico", Titulo = "¿Qué se va a agotar primero?",      Icono = "bi-exclamation-triangle", Color = "red",    Categoria = "Stock" },
            new() { Id = "valor_total",   Titulo = "¿Cuánto vale el inventario?",       Icono = "bi-currency-dollar",    Color = "green",  Categoria = "Financiero" },
            new() { Id = "top_bienes",    Titulo = "Top 5 bienes más caros",            Icono = "bi-trophy",             Color = "orange", Categoria = "Bienes" },
            new() { Id = "sin_responsable", Titulo = "¿Qué bienes no tienen responsable?", Icono = "bi-person-dash",      Color = "purple", Categoria = "Control" },
            new() { Id = "sin_ubicacion", Titulo = "¿Qué bienes no tienen ubicación?",  Icono = "bi-geo-alt",            Color = "purple", Categoria = "Control" },
            new() { Id = "categorias",    Titulo = "Distribución por categoría",        Icono = "bi-pie-chart",          Color = "blue",   Categoria = "Análisis" },
            new() { Id = "anomalias",     Titulo = "¿Hay valores anómalos?",            Icono = "bi-exclamation-circle", Color = "red",    Categoria = "Análisis" },
            new() { Id = "prestamos",     Titulo = "¿Hay préstamos vencidos?",          Icono = "bi-clock-history",      Color = "orange", Categoria = "Movimientos" },
            new() { Id = "ranking",       Titulo = "Ranking de responsables",           Icono = "bi-people",             Color = "green",  Categoria = "Análisis" }
        };
    }

    // ═══════════════════════════════════════════════════════════
    // RESOLVER PREGUNTA (motor principal)
    // ═══════════════════════════════════════════════════════════
    public async Task<RespuestaAsistenteDTO> ResolverAsync(string idPregunta, int institucionId)
    {
        try
        {
            return idPregunta switch
            {
                "resumen" => await ResumenGeneralAsync(institucionId),
                "stock_critico" => await StockCriticoAsync(institucionId),
                "valor_total" => await ValorTotalAsync(institucionId),
                "top_bienes" => await TopBienesAsync(institucionId),
                "sin_responsable" => await SinResponsableAsync(institucionId),
                "sin_ubicacion" => await SinUbicacionAsync(institucionId),
                "categorias" => await CategoriasAsync(institucionId),
                "anomalias" => await AnomaliasAsync(institucionId),
                "prestamos" => await PrestamosVencidosAsync(institucionId),
                "ranking" => await RankingAsync(institucionId),
                _ => RespuestaNoEncontrada()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolviendo pregunta {Id}", idPregunta);
            return new RespuestaAsistenteDTO
            {
                Pregunta = idPregunta,
                Respuesta = "Lo siento, hubo un error consultando la información. Intenta de nuevo.",
                TipoRespuesta = "danger",
                Exito = false
            };
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 1. RESUMEN GENERAL
    // ═══════════════════════════════════════════════════════════
    private async Task<RespuestaAsistenteDTO> ResumenGeneralAsync(int institucionId)
    {
        const string sql = @"SELECT * FROM v_asistente_resumen WHERE institucion_id = @InstitucionId";

        await using var db = await CrearConexionAsync();
        var r = await db.QueryFirstOrDefaultAsync<AsistenteResumenDTO>(sql, new { InstitucionId = institucionId });

        if (r is null)
            return new RespuestaAsistenteDTO { Respuesta = "No hay datos disponibles.", TipoRespuesta = "warning" };

        var texto = $@"📊 **Resumen del Inventario**

🎯 **Bienes activos:** {r.TotalDevolutivos + r.TotalConsumibles} ({r.TotalDevolutivos} devolutivos + {r.TotalConsumibles} consumibles)
📉 **Bajas registradas:** {r.TotalBajas}
💰 **Valor total:** {FormatearMoneda(r.ValorTotalInventario)}
📊 **Depreciación acumulada:** {FormatearMoneda(r.DepreciacionTotal)}
💎 **Valor neto:** {FormatearMoneda(r.ValorNetoTotal)}

⚠️ **Alertas de control:**
• {r.SinResponsable} bienes sin responsable
• {r.SinUbicacion} bienes sin ubicación";

        return new RespuestaAsistenteDTO
        {
            Pregunta = "resumen",
            Respuesta = texto,
            TipoRespuesta = "info",
            Sugerencia = "Prueba: ¿Qué se va a agotar primero?"
        };
    }

    // ═══════════════════════════════════════════════════════════
    // 2. STOCK CRÍTICO
    // ═══════════════════════════════════════════════════════════
    private async Task<RespuestaAsistenteDTO> StockCriticoAsync(int institucionId)
    {
        const string sql = @"
            SELECT codigo, nombre, stock_actual, stock_minimo, faltante, severidad
            FROM v_alertas_stock
            WHERE institucion_id = @InstitucionId
            ORDER BY 
                CASE severidad WHEN 'CRITICO' THEN 1 WHEN 'ALTO' THEN 2 ELSE 3 END,
                faltante DESC
            LIMIT 10";

        await using var db = await CrearConexionAsync();
        var items = (await db.QueryAsync(sql, new { InstitucionId = institucionId })).ToList();

        if (!items.Any())
        {
            return new RespuestaAsistenteDTO
            {
                Pregunta = "stock_critico",
                Respuesta = "✅ ¡Excelente! No hay consumibles con stock crítico en este momento.",
                TipoRespuesta = "success"
            };
        }

        var criticos = items.Count(x => ((string)x.severidad) == "CRITICO");
        var altos = items.Count(x => ((string)x.severidad) == "ALTO");

        var respuesta = $"⚠️ **{items.Count} consumibles requieren atención** ({criticos} críticos, {altos} altos). Aquí los más urgentes:";

        var tabla = new RespuestaTablaDTO
        {
            Columnas = new List<string> { "Código", "Producto", "Stock", "Mínimo", "Faltante", "Severidad" }
        };

        foreach (var item in items.Take(5))
        {
            tabla.Filas.Add(new List<string>
            {
                item.codigo?.ToString() ?? "",
                item.nombre?.ToString() ?? "",
                item.stock_actual?.ToString() ?? "0",
                item.stock_minimo?.ToString() ?? "0",
                item.faltante?.ToString() ?? "0",
                item.severidad?.ToString() ?? ""
            });
        }

        return new RespuestaAsistenteDTO
        {
            Pregunta = "stock_critico",
            Respuesta = respuesta,
            TipoRespuesta = "warning",
            Tabla = new List<RespuestaTablaDTO> { tabla },
            Sugerencia = "Ve a INTELIGENCIA → Alertas IA para más detalle"
        };
    }

    // ═══════════════════════════════════════════════════════════
    // 3. VALOR TOTAL
    // ═══════════════════════════════════════════════════════════
    private async Task<RespuestaAsistenteDTO> ValorTotalAsync(int institucionId)
    {
        const string sql = @"SELECT * FROM v_asistente_resumen WHERE institucion_id = @InstitucionId";

        await using var db = await CrearConexionAsync();
        var r = await db.QueryFirstOrDefaultAsync<AsistenteResumenDTO>(sql, new { InstitucionId = institucionId });

        if (r is null)
            return new RespuestaAsistenteDTO { Respuesta = "No hay datos.", TipoRespuesta = "warning" };

        var texto = $@"💰 **Valor del Inventario**

📊 **Valor de adquisición:** {FormatearMoneda(r.ValorTotalInventario)}
📉 **Depreciación acumulada:** {FormatearMoneda(r.DepreciacionTotal)}
💎 **Valor neto contable:** {FormatearMoneda(r.ValorNetoTotal)}

El valor neto es lo que vale el inventario hoy después de depreciación.";

        return new RespuestaAsistenteDTO
        {
            Pregunta = "valor_total",
            Respuesta = texto,
            TipoRespuesta = "success"
        };
    }

    // ═══════════════════════════════════════════════════════════
    // 4. TOP 5 BIENES MÁS CAROS
    // ═══════════════════════════════════════════════════════════
    private async Task<RespuestaAsistenteDTO> TopBienesAsync(int institucionId)
    {
        const string sql = @"
            SELECT b.codigo, b.nombre, c.nombre AS categoria, b.valor_adquisicion, b.valor_neto
            FROM bienes b
            LEFT JOIN categorias c ON c.id = b.categoria_id
            WHERE b.institucion_id = @InstitucionId 
              AND b.activo = TRUE 
              AND b.tipo_bien = 'devolutivo'
            ORDER BY b.valor_adquisicion DESC
            LIMIT 5";

        await using var db = await CrearConexionAsync();
        var items = (await db.QueryAsync(sql, new { InstitucionId = institucionId })).ToList();

        if (!items.Any())
            return new RespuestaAsistenteDTO { Respuesta = "No hay bienes activos.", TipoRespuesta = "warning" };

        var tabla = new RespuestaTablaDTO
        {
            Columnas = new List<string> { "#", "Código", "Bien", "Valor Adq.", "Valor Neto" }
        };

        int i = 1;
        foreach (var item in items)
        {
            tabla.Filas.Add(new List<string>
            {
                i.ToString(),
                item.codigo?.ToString() ?? "",
                item.nombre?.ToString() ?? "",
                FormatearMoneda(Convert.ToDecimal(item.valor_adquisicion ?? 0)),
                FormatearMoneda(Convert.ToDecimal(item.valor_neto ?? 0))
            });
            i++;
        }

        return new RespuestaAsistenteDTO
        {
            Pregunta = "top_bienes",
            Respuesta = "🏆 **Top 5 bienes más caros del inventario**",
            TipoRespuesta = "info",
            Tabla = new List<RespuestaTablaDTO> { tabla }
        };
    }

    // ═══════════════════════════════════════════════════════════
    // 5. SIN RESPONSABLE
    // ═══════════════════════════════════════════════════════════
    private async Task<RespuestaAsistenteDTO> SinResponsableAsync(int institucionId)
    {
        const string sql = @"
            SELECT codigo, nombre, valor_adquisicion, categoria, aula
            FROM v_alertas_sin_responsable
            WHERE institucion_id = @InstitucionId
            ORDER BY valor_adquisicion DESC
            LIMIT 10";

        await using var db = await CrearConexionAsync();
        var items = (await db.QueryAsync(sql, new { InstitucionId = institucionId })).ToList();

        if (!items.Any())
        {
            return new RespuestaAsistenteDTO
            {
                Pregunta = "sin_responsable",
                Respuesta = "✅ ¡Perfecto! Todos los bienes tienen responsable asignado.",
                TipoRespuesta = "success"
            };
        }

        var respuesta = $"⚠️ **Hay {items.Count} bienes activos sin responsable asignado.** Se recomienda asignarlos para tener control:";

        var tabla = new RespuestaTablaDTO
        {
            Columnas = new List<string> { "Código", "Bien", "Categoría", "Ubicación", "Valor" }
        };

        foreach (var item in items.Take(5))
        {
            tabla.Filas.Add(new List<string>
            {
                item.codigo?.ToString() ?? "",
                item.nombre?.ToString() ?? "",
                item.categoria?.ToString() ?? "—",
                item.aula?.ToString() ?? "—",
                FormatearMoneda(Convert.ToDecimal(item.valor_adquisicion ?? 0))
            });
        }

        return new RespuestaAsistenteDTO
        {
            Pregunta = "sin_responsable",
            Respuesta = respuesta,
            TipoRespuesta = "warning",
            Tabla = new List<RespuestaTablaDTO> { tabla }
        };
    }

    // ═══════════════════════════════════════════════════════════
    // 6. SIN UBICACIÓN
    // ═══════════════════════════════════════════════════════════
    private async Task<RespuestaAsistenteDTO> SinUbicacionAsync(int institucionId)
    {
        const string sql = @"
            SELECT codigo, nombre, valor_adquisicion, categoria, responsable
            FROM v_alertas_sin_ubicacion
            WHERE institucion_id = @InstitucionId
            ORDER BY valor_adquisicion DESC
            LIMIT 10";

        await using var db = await CrearConexionAsync();
        var items = (await db.QueryAsync(sql, new { InstitucionId = institucionId })).ToList();

        if (!items.Any())
        {
            return new RespuestaAsistenteDTO
            {
                Pregunta = "sin_ubicacion",
                Respuesta = "✅ ¡Perfecto! Todos los bienes tienen ubicación asignada.",
                TipoRespuesta = "success"
            };
        }

        var respuesta = $"⚠️ **Hay {items.Count} bienes activos sin ubicación asignada:**";

        var tabla = new RespuestaTablaDTO
        {
            Columnas = new List<string> { "Código", "Bien", "Categoría", "Responsable", "Valor" }
        };

        foreach (var item in items.Take(5))
        {
            tabla.Filas.Add(new List<string>
            {
                item.codigo?.ToString() ?? "",
                item.nombre?.ToString() ?? "",
                item.categoria?.ToString() ?? "—",
                item.responsable?.ToString() ?? "—",
                FormatearMoneda(Convert.ToDecimal(item.valor_adquisicion ?? 0))
            });
        }

        return new RespuestaAsistenteDTO
        {
            Pregunta = "sin_ubicacion",
            Respuesta = respuesta,
            TipoRespuesta = "warning",
            Tabla = new List<RespuestaTablaDTO> { tabla }
        };
    }

    // ═══════════════════════════════════════════════════════════
    // 7. DISTRIBUCIÓN POR CATEGORÍA
    // ═══════════════════════════════════════════════════════════
    private async Task<RespuestaAsistenteDTO> CategoriasAsync(int institucionId)
    {
        const string sql = @"
            SELECT categoria_nombre, codigo_cgn, tipo_bien, cantidad, valor_total, depreciacion_total
            FROM v_distribucion_categoria
            WHERE institucion_id = @InstitucionId
            ORDER BY valor_total DESC";

        await using var db = await CrearConexionAsync();
        var items = (await db.QueryAsync(sql, new { InstitucionId = institucionId })).ToList();

        if (!items.Any())
            return new RespuestaAsistenteDTO { Respuesta = "No hay categorías registradas.", TipoRespuesta = "warning" };

        var respuesta = $"📊 **Distribución del inventario en {items.Count} categorías CGN**";

        var tabla = new RespuestaTablaDTO
        {
            Columnas = new List<string> { "CGN", "Categoría", "Tipo", "Cant.", "Valor Total" }
        };

        foreach (var item in items.Take(10))
        {
            tabla.Filas.Add(new List<string>
            {
                item.codigo_cgn?.ToString() ?? "",
                item.categoria_nombre?.ToString() ?? "",
                item.tipo_bien?.ToString() ?? "",
                item.cantidad?.ToString() ?? "0",
                FormatearMoneda(Convert.ToDecimal(item.valor_total ?? 0))
            });
        }

        return new RespuestaAsistenteDTO
        {
            Pregunta = "categorias",
            Respuesta = respuesta,
            TipoRespuesta = "info",
            Tabla = new List<RespuestaTablaDTO> { tabla }
        };
    }

    // ═══════════════════════════════════════════════════════════
    // 8. ANOMALÍAS
    // ═══════════════════════════════════════════════════════════
    private async Task<RespuestaAsistenteDTO> AnomaliasAsync(int institucionId)
    {
        const string sql = @"
            SELECT codigo, nombre, valor_adquisicion, media, z_score, clasificacion
            FROM v_anomalias_valor
            WHERE institucion_id = @InstitucionId 
              AND clasificacion != 'NORMAL'
            ORDER BY ABS(z_score) DESC
            LIMIT 10";

        await using var db = await CrearConexionAsync();
        var items = (await db.QueryAsync(sql, new { InstitucionId = institucionId })).ToList();

        if (!items.Any())
        {
            return new RespuestaAsistenteDTO
            {
                Pregunta = "anomalias",
                Respuesta = "✅ No se detectaron anomalías de valor. Todos los bienes están dentro del rango esperado.",
                TipoRespuesta = "success"
            };
        }

        var respuesta = $"⚠️ **Se detectaron {items.Count} valores anómalos.** Se recomienda revisar si están correctamente registrados:";

        var tabla = new RespuestaTablaDTO
        {
            Columnas = new List<string> { "Código", "Bien", "Valor", "Media", "Z-Score", "Clasificación" }
        };

        foreach (var item in items)
        {
            tabla.Filas.Add(new List<string>
            {
                item.codigo?.ToString() ?? "",
                item.nombre?.ToString() ?? "",
                FormatearMoneda(Convert.ToDecimal(item.valor_adquisicion ?? 0)),
                FormatearMoneda(Convert.ToDecimal(item.media ?? 0)),
                Convert.ToDecimal(item.z_score ?? 0).ToString("N2"),
                item.clasificacion?.ToString() ?? ""
            });
        }

        return new RespuestaAsistenteDTO
        {
            Pregunta = "anomalias",
            Respuesta = respuesta,
            TipoRespuesta = "warning",
            Tabla = new List<RespuestaTablaDTO> { tabla }
        };
    }

    // ═══════════════════════════════════════════════════════════
    // 9. PRÉSTAMOS VENCIDOS
    // ═══════════════════════════════════════════════════════════
    private async Task<RespuestaAsistenteDTO> PrestamosVencidosAsync(int institucionId)
    {
        const string sql = @"
            SELECT bien_codigo, bien_nombre, funcionario, fecha_devolucion_prevista, dias_retraso, severidad
            FROM v_alertas_prestamos
            WHERE institucion_id = @InstitucionId
            ORDER BY dias_retraso DESC
            LIMIT 10";

        await using var db = await CrearConexionAsync();
        var items = (await db.QueryAsync(sql, new { InstitucionId = institucionId })).ToList();

        if (!items.Any())
        {
            return new RespuestaAsistenteDTO
            {
                Pregunta = "prestamos",
                Respuesta = "✅ ¡Excelente! No hay préstamos vencidos actualmente.",
                TipoRespuesta = "success"
            };
        }

        var respuesta = $"⏰ **Hay {items.Count} préstamos vencidos.** Contacta a los responsables:";

        var tabla = new RespuestaTablaDTO
        {
            Columnas = new List<string> { "Bien", "Funcionario", "Vencido", "Días", "Severidad" }
        };

        foreach (var item in items)
        {
            tabla.Filas.Add(new List<string>
            {
                item.bien_nombre?.ToString() ?? "",
                item.funcionario?.ToString() ?? "",
                Convert.ToDateTime(item.fecha_devolucion_prevista).ToString("dd/MM/yyyy"),
                "+" + item.dias_retraso?.ToString() + " días",
                item.severidad?.ToString() ?? ""
            });
        }

        return new RespuestaAsistenteDTO
        {
            Pregunta = "prestamos",
            Respuesta = respuesta,
            TipoRespuesta = "danger",
            Tabla = new List<RespuestaTablaDTO> { tabla }
        };
    }

    // ═══════════════════════════════════════════════════════════
    // 10. RANKING DE RESPONSABLES
    // ═══════════════════════════════════════════════════════════
    private async Task<RespuestaAsistenteDTO> RankingAsync(int institucionId)
    {
        const string sql = @"
            SELECT funcionario, cargo, total_bienes, valor_total
            FROM v_ranking_responsables
            WHERE institucion_id = @InstitucionId
            ORDER BY total_bienes DESC, valor_total DESC
            LIMIT 10";

        await using var db = await CrearConexionAsync();
        var items = (await db.QueryAsync(sql, new { InstitucionId = institucionId })).ToList();

        if (!items.Any())
            return new RespuestaAsistenteDTO { Respuesta = "No hay responsables con bienes asignados.", TipoRespuesta = "info" };

        var tabla = new RespuestaTablaDTO
        {
            Columnas = new List<string> { "#", "Funcionario", "Cargo", "Bienes", "Valor Total" }
        };

        int i = 1;
        foreach (var item in items)
        {
            tabla.Filas.Add(new List<string>
            {
                i.ToString(),
                item.funcionario?.ToString() ?? "",
                item.cargo?.ToString() ?? "",
                item.total_bienes?.ToString() ?? "0",
                FormatearMoneda(Convert.ToDecimal(item.valor_total ?? 0))
            });
            i++;
        }

        return new RespuestaAsistenteDTO
        {
            Pregunta = "ranking",
            Respuesta = "🏆 **Ranking de responsables por bienes asignados**",
            TipoRespuesta = "info",
            Tabla = new List<RespuestaTablaDTO> { tabla }
        };
    }

    // ═══════════════════════════════════════════════════════════
    // UTILIDADES
    // ═══════════════════════════════════════════════════════════
    private static RespuestaAsistenteDTO RespuestaNoEncontrada()
    {
        return new RespuestaAsistenteDTO
        {
            Respuesta = "No entendí esa pregunta. Prueba con las tarjetas de arriba o con alguna de estas:\n\n" +
                        "• ¿Qué se va a agotar primero?\n" +
                        "• Dame un resumen del inventario\n" +
                        "• ¿Qué bienes no tienen responsable?",
            TipoRespuesta = "warning",
            Exito = false,
            Sugerencia = "Elige una de las tarjetas rápidas"
        };
    }

    private static string FormatearMoneda(decimal valor)
        => valor.ToString("C0", new System.Globalization.CultureInfo("es-CO"));

    public async Task RefrescarVistasAsync()
    {
        const string sql = @"REFRESH MATERIALIZED VIEW v_asistente_resumen;";
        await using var db = await CrearConexionAsync();
        await db.ExecuteAsync(sql);
    }
}