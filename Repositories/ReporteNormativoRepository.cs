using Almacen.DTOs;
using Almacen.Interfaces;
using Dapper;
using Npgsql;

namespace Almacen.Repositories;

public class ReporteNormativoRepository : IReporteNormativoRepository
{
    private readonly string _cs;

    public ReporteNormativoRepository(IConfiguration cfg)
    {
        _cs = cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta DefaultConnection.");
    }

    private const string BaseSelect = @"
        SELECT
            id,
            entidad_receptora   AS EntidadReceptora,
            nombre_reporte      AS NombreReporte,
            descripcion,
            sujeto_obligado     AS SujetoObligado,
            periodicidad,
            fecha_limite        AS FechaLimite,
            vigencia_desde::timestamp AS VigenciaDesde,
            vigencia_hasta::timestamp AS VigenciaHasta,
            version_formato     AS VersionFormato,
            formato_salida      AS FormatoSalida,
            campos_requeridos   AS CamposRequeridos,
            reglas_validacion   AS ReglasValidacion,
            fuente_oficial      AS FuenteOficial,
            url_oficial         AS UrlOficial,
            responsable_interno AS ResponsableInterno,
            estado,
            plantilla_ruta      AS PlantillaRuta,
            created_at          AS CreatedAt,
            updated_at          AS UpdatedAt
        FROM configuracion_reportes_normativos";

    public async Task<IEnumerable<ReporteNormativoDTO>> ObtenerTodosAsync(
        string? filtroTexto = null,
        string? filtroEstado = null,
        string? filtroPeriodicidad = null)
    {
        var condiciones = new List<string> { "1=1" };
        var parametros = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(filtroTexto))
        {
            condiciones.Add(@"(entidad_receptora ILIKE @Buscar 
                             OR nombre_reporte ILIKE @Buscar 
                             OR descripcion ILIKE @Buscar 
                             OR sujeto_obligado ILIKE @Buscar)");
            parametros.Add("Buscar", $"%{filtroTexto}%");
        }

        if (!string.IsNullOrWhiteSpace(filtroEstado))
        {
            condiciones.Add("estado = @Estado");
            parametros.Add("Estado", filtroEstado);
        }

        if (!string.IsNullOrWhiteSpace(filtroPeriodicidad))
        {
            condiciones.Add("periodicidad = @Periodicidad");
            parametros.Add("Periodicidad", filtroPeriodicidad);
        }

        var whereExtra = string.Join(" AND ", condiciones);

        var sql = $@"{BaseSelect}
                     WHERE {whereExtra}
                     ORDER BY 
                        CASE estado
                            WHEN 'VIGENTE'  THEN 1
                            WHEN 'BORRADOR' THEN 2
                            WHEN 'RETIRADO' THEN 3
                            ELSE 4
                        END,
                        entidad_receptora,
                        nombre_reporte;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<ReporteNormativoDTO>(sql, parametros);
    }

    public async Task<ReporteNormativoDTO?> ObtenerPorIdAsync(int id)
    {
        var sql = $"{BaseSelect} WHERE id = @Id;";
        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<ReporteNormativoDTO>(sql, new { Id = id });
    }

    public async Task<int> CrearAsync(ReporteNormativoDTO dto)
    {
        const string sql = @"
            INSERT INTO configuracion_reportes_normativos
                (entidad_receptora, nombre_reporte, descripcion, sujeto_obligado,
                 periodicidad, fecha_limite, vigencia_desde, vigencia_hasta,
                 version_formato, formato_salida, campos_requeridos, reglas_validacion,
                 fuente_oficial, url_oficial, responsable_interno, estado,
                 plantilla_ruta, created_at, updated_at)
            VALUES
                (@EntidadReceptora, @NombreReporte, @Descripcion, @SujetoObligado,
                 @Periodicidad, @FechaLimite, @VigenciaDesde, @VigenciaHasta,
                 @VersionFormato, @FormatoSalida, @CamposRequeridos, @ReglasValidacion,
                 @FuenteOficial, @UrlOficial, @ResponsableInterno, @Estado,
                 @PlantillaRuta, NOW(), NOW())
            RETURNING id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, dto);
    }

    public async Task ActualizarAsync(ReporteNormativoDTO dto)
    {
        const string sql = @"
            UPDATE configuracion_reportes_normativos
            SET entidad_receptora = @EntidadReceptora,
                nombre_reporte = @NombreReporte,
                descripcion = @Descripcion,
                sujeto_obligado = @SujetoObligado,
                periodicidad = @Periodicidad,
                fecha_limite = @FechaLimite,
                vigencia_desde = @VigenciaDesde,
                vigencia_hasta = @VigenciaHasta,
                version_formato = @VersionFormato,
                formato_salida = @FormatoSalida,
                campos_requeridos = @CamposRequeridos,
                reglas_validacion = @ReglasValidacion,
                fuente_oficial = @FuenteOficial,
                url_oficial = @UrlOficial,
                responsable_interno = @ResponsableInterno,
                estado = @Estado,
                updated_at = NOW()
            WHERE id = @Id;";

        using var cn = new NpgsqlConnection(_cs);
        await cn.ExecuteAsync(sql, dto);
    }

    public async Task CambiarEstadoAsync(int id, string nuevoEstado)
    {
        const string sql = @"
            UPDATE configuracion_reportes_normativos
            SET estado = @Estado, updated_at = NOW()
            WHERE id = @Id;";

        using var cn = new NpgsqlConnection(_cs);
        await cn.ExecuteAsync(sql, new { Id = id, Estado = nuevoEstado });
    }

    public async Task ActualizarPlantillaAsync(int id, string? rutaPlantilla)
    {
        const string sql = @"
            UPDATE configuracion_reportes_normativos
            SET plantilla_ruta = @Ruta, updated_at = NOW()
            WHERE id = @Id;";

        using var cn = new NpgsqlConnection(_cs);
        await cn.ExecuteAsync(sql, new { Id = id, Ruta = rutaPlantilla });
    }

    public async Task EliminarAsync(int id)
    {
        const string sql = @"DELETE FROM configuracion_reportes_normativos WHERE id = @Id;";
        using var cn = new NpgsqlConnection(_cs);
        await cn.ExecuteAsync(sql, new { Id = id });
    }

    public async Task<(int Borradores, int Vigentes, int Retirados, int Total)> ObtenerMetricasAsync()
    {
        const string sql = @"
            SELECT
                COUNT(*) FILTER (WHERE estado = 'BORRADOR')::int AS Borradores,
                COUNT(*) FILTER (WHERE estado = 'VIGENTE')::int  AS Vigentes,
                COUNT(*) FILTER (WHERE estado = 'RETIRADO')::int AS Retirados,
                COUNT(*)::int                                    AS Total
            FROM configuracion_reportes_normativos;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstAsync<(int Borradores, int Vigentes, int Retirados, int Total)>(sql);
    }
}