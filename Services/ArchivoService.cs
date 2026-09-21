using Dapper;
using Npgsql;

namespace Almacen.Services;

public class ArchivoAdjuntoDTO
{
    public int Id { get; set; }
    public string EntidadTipo { get; set; } = string.Empty;
    public int EntidadId { get; set; }
    public string NombreOriginal { get; set; } = string.Empty;
    public string RutaAlmacen { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public long? TamanoBytes { get; set; }
    public DateTime CreatedAt { get; set; }

    public string TamanoFormateado => TamanoBytes switch
    {
        < 1024 => $"{TamanoBytes} B",
        < 1024 * 1024 => $"{TamanoBytes / 1024.0:F1} KB",
        _ => $"{TamanoBytes / (1024.0 * 1024):F1} MB"
    };
}

/// <summary>
/// Gestiona archivos adjuntos (PDFs, imágenes) en el sistema de archivos
/// y su metadata en la tabla archivos_adjuntos.
/// </summary>
public class ArchivoService
{
    private readonly string _carpetaBase;
    private readonly NpgsqlConnectionFactory _factory;

    // Tamaño máximo: 10 MB
    public const long TamanoMaximoBytes = 10 * 1024 * 1024;

    // Tipos permitidos
    private static readonly string[] MimesPermitidos =
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/jpg"
    };

    public ArchivoService(IWebHostEnvironment env, NpgsqlConnectionFactory factory)
    {
        _carpetaBase = Path.Combine(env.WebRootPath, "uploads");
        _factory = factory;

        if (!Directory.Exists(_carpetaBase))
            Directory.CreateDirectory(_carpetaBase);
    }

    /// <summary>
    /// Valida un archivo antes de subirlo.
    /// </summary>
    public (bool Ok, string Mensaje) Validar(string nombre, long tamano, string? mimeType)
    {
        if (tamano <= 0)
            return (false, "El archivo está vacío.");

        if (tamano > TamanoMaximoBytes)
            return (false, $"El archivo excede el tamaño máximo de 10 MB.");

        if (!string.IsNullOrWhiteSpace(mimeType) && !MimesPermitidos.Contains(mimeType))
            return (false, "Solo se permiten archivos PDF, JPG o PNG.");

        var extension = Path.GetExtension(nombre).ToLowerInvariant();
        if (extension != ".pdf" && extension != ".jpg"
            && extension != ".jpeg" && extension != ".png")
        {
            return (false, "Extensión no permitida. Use PDF, JPG o PNG.");
        }

        return (true, "");
    }

    /// <summary>
    /// Guarda un archivo físico + metadata en BD.
    /// </summary>
    public async Task<int> GuardarAsync(
        Stream contenido,
        string nombreOriginal,
        string mimeType,
        string entidadTipo,
        int entidadId)
    {
        // Generar nombre único
        var extension = Path.GetExtension(nombreOriginal);
        var nombreUnico = $"{Guid.NewGuid():N}{extension}";

        // Subcarpeta por entidad_tipo (ej: entrada, salida)
        var subCarpeta = Path.Combine(_carpetaBase, entidadTipo);
        if (!Directory.Exists(subCarpeta))
            Directory.CreateDirectory(subCarpeta);

        var rutaFisica = Path.Combine(subCarpeta, nombreUnico);
        var rutaRelativa = $"/uploads/{entidadTipo}/{nombreUnico}";

        // Guardar archivo físico
        using (var fs = new FileStream(rutaFisica, FileMode.Create))
        {
            await contenido.CopyToAsync(fs);
        }

        var info = new FileInfo(rutaFisica);

        // Guardar metadata en BD
        using var cn = _factory.CrearConexion();
        const string sql = @"
            INSERT INTO archivos_adjuntos
                (entidad_tipo, entidad_id, nombre_original, ruta_almacen,
                 mime_type, tamano_bytes)
            VALUES
                (@EntidadTipo, @EntidadId, @NombreOriginal, @Ruta,
                 @MimeType, @TamanoBytes)
            RETURNING id;";

        return await cn.ExecuteScalarAsync<int>(sql, new
        {
            EntidadTipo = entidadTipo,
            EntidadId = entidadId,
            NombreOriginal = nombreOriginal,
            Ruta = rutaRelativa,
            MimeType = mimeType,
            TamanoBytes = info.Length
        });
    }

    /// <summary>
    /// Lista los adjuntos de una entidad.
    /// </summary>
    public async Task<IEnumerable<ArchivoAdjuntoDTO>> ObtenerAsync(
        string entidadTipo, int entidadId)
    {
        const string sql = @"
            SELECT id, entidad_tipo AS EntidadTipo, entidad_id AS EntidadId,
                   nombre_original AS NombreOriginal, ruta_almacen AS RutaAlmacen,
                   mime_type AS MimeType, tamano_bytes AS TamanoBytes,
                   created_at AS CreatedAt
            FROM archivos_adjuntos
            WHERE entidad_tipo = @EntidadTipo AND entidad_id = @EntidadId
            ORDER BY created_at DESC;";

        using var cn = _factory.CrearConexion();
        return await cn.QueryAsync<ArchivoAdjuntoDTO>(sql,
            new { EntidadTipo = entidadTipo, EntidadId = entidadId });
    }

    /// <summary>
    /// Elimina un adjunto (físico + BD).
    /// </summary>
    public async Task<bool> EliminarAsync(int id)
    {
        // Buscar
        const string sqlGet = @"SELECT ruta_almacen FROM archivos_adjuntos WHERE id = @Id;";
        using var cn = _factory.CrearConexion();
        var rutaRelativa = await cn.ExecuteScalarAsync<string?>(sqlGet, new { Id = id });
        if (rutaRelativa is null) return false;

        // Borrar archivo físico
        var rutaFisica = Path.Combine(_carpetaBase,
            rutaRelativa.Replace("/uploads/", "").Replace("/", Path.DirectorySeparatorChar.ToString()));

        if (File.Exists(rutaFisica))
        {
            try { File.Delete(rutaFisica); }
            catch { /* ignorar si está en uso */ }
        }

        // Borrar BD
        const string sqlDel = @"DELETE FROM archivos_adjuntos WHERE id = @Id;";
        return await cn.ExecuteAsync(sqlDel, new { Id = id }) > 0;
    }

    /// <summary>
    /// Lee el contenido de un archivo para descargarlo.
    /// </summary>
    public async Task<(byte[] Bytes, string Nombre, string Mime)?> LeerAsync(int id)
    {
        const string sql = @"
            SELECT nombre_original AS Nombre, ruta_almacen AS Ruta,
                   mime_type AS Mime
            FROM archivos_adjuntos WHERE id = @Id;";

        using var cn = _factory.CrearConexion();
        var row = await cn.QueryFirstOrDefaultAsync<(string Nombre, string Ruta, string? Mime)>(
            sql, new { Id = id });

        if (string.IsNullOrEmpty(row.Ruta)) return null;

        var rutaFisica = Path.Combine(_carpetaBase,
            row.Ruta.Replace("/uploads/", "").Replace("/", Path.DirectorySeparatorChar.ToString()));

        if (!File.Exists(rutaFisica)) return null;

        var bytes = await File.ReadAllBytesAsync(rutaFisica);
        return (bytes, row.Nombre, row.Mime ?? "application/octet-stream");
    }
}

/// <summary>
/// Fábrica simple de conexiones Npgsql para servicios.
/// </summary>
public class NpgsqlConnectionFactory
{
    private readonly string _cs;
    public NpgsqlConnectionFactory(IConfiguration cfg)
        => _cs = cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta DefaultConnection.");

    public NpgsqlConnection CrearConexion() => new NpgsqlConnection(_cs);
}