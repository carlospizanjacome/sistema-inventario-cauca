using Almacen.DTOs;
using Almacen.Helpers;
using Almacen.Interfaces;
using Almacen.Services;
using Dapper;
using Npgsql;

namespace Almacen.Repositories;

public class FuncionarioRepository : IFuncionarioRepository
{
    private readonly string _cs;
    private readonly UsuarioSesionService _sesion;

    public FuncionarioRepository(IConfiguration cfg, UsuarioSesionService sesion)
    {
        _cs = cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta DefaultConnection.");
        _sesion = sesion;
    }

    private const string BaseSelect = @"
        SELECT f.id, f.cedula, f.nombre_completo AS NombreCompleto,
               f.cargo, f.tipo_vinculacion AS TipoVinculacion,
               f.email, f.telefono, f.sede_id AS SedeId, f.usuario_id AS UsuarioId,
               f.activo, f.created_at AS CreatedAt,
               f.institucion_id AS InstitucionId,
               s.nombre AS SedeNombre,
               i.nombre AS InstitucionNombre,
               u.email  AS UsuarioEmail
        FROM funcionarios f
        LEFT JOIN sedes s ON s.id = f.sede_id
        LEFT JOIN instituciones i ON i.id = f.institucion_id
        LEFT JOIN usuarios u ON u.id = f.usuario_id";

    public async Task<IEnumerable<FuncionarioDTO>> ObtenerTodosAsync()
    {
        var (filtro, param) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "f");

        var sql = $"{BaseSelect} WHERE 1=1 {filtro} ORDER BY f.nombre_completo;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<FuncionarioDTO>(sql, param);
    }

    public async Task<FuncionarioDTO?> ObtenerPorIdAsync(int id)
    {
        var (filtro, param) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "f");

        var sql = $"{BaseSelect} WHERE f.id = @Id {filtro};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<FuncionarioDTO>(sql,
            new { Id = id, InstitucionId = _sesion.InstitucionId });
    }

    public async Task<int> CrearAsync(FuncionarioDTO dto)
    {
        // Asignar institución del usuario actual
        dto.InstitucionId = _sesion.InstitucionId;

        const string sql = @"
            INSERT INTO funcionarios
                (cedula, nombre_completo, cargo, tipo_vinculacion, email, telefono,
                 sede_id, usuario_id, activo, institucion_id)
            VALUES
                (@Cedula, @NombreCompleto, @Cargo, @TipoVinculacion, @Email, @Telefono,
                 @SedeId, @UsuarioId, @Activo, @InstitucionId)
            RETURNING id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, dto);
    }

    public async Task<bool> ActualizarAsync(FuncionarioDTO dto)
    {
        const string sql = @"
            UPDATE funcionarios
            SET cedula = @Cedula, nombre_completo = @NombreCompleto,
                cargo = @Cargo, tipo_vinculacion = @TipoVinculacion,
                email = @Email, telefono = @Telefono,
                sede_id = @SedeId, usuario_id = @UsuarioId,
                activo = @Activo, updated_at = NOW()
            WHERE id = @Id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync(sql, dto) > 0;
    }

    public async Task<bool> EliminarAsync(int id)
    {
        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync("DELETE FROM funcionarios WHERE id = @Id;",
            new { Id = id }) > 0;
    }

    public async Task<bool> ExisteCedulaAsync(string cedula, int? excluirId = null)
    {
        var (filtro, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "f");

        var sql = $@"
            SELECT COUNT(*) FROM funcionarios f
            WHERE f.cedula = @Cedula
              AND (@ExcluirId IS NULL OR f.id <> @ExcluirId)
              {filtro};";

        using var cn = new NpgsqlConnection(_cs);
        var n = await cn.ExecuteScalarAsync<int>(sql,
            new { Cedula = cedula, ExcluirId = excluirId, InstitucionId = _sesion.InstitucionId });
        return n > 0;
    }

    public async Task<bool> TieneBienesAsync(int funcionarioId)
    {
        using var cn = new NpgsqlConnection(_cs);
        var n = await cn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM bienes WHERE funcionario_id = @Id;",
            new { Id = funcionarioId });
        return n > 0;
    }

    public async Task<ResultadoPaginado<FuncionarioDTO>> ObtenerPaginadoAsync(
        int pagina = 1,
        int tamano = 25,
        string? filtroTexto = null)
    {
        if (pagina < 1) pagina = 1;
        if (tamano < 1) tamano = 25;
        if (tamano > 200) tamano = 200;

        var (filtroInst, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "f");

        var filtroBusqueda = string.Empty;
        if (!string.IsNullOrWhiteSpace(filtroTexto))
        {
            filtroBusqueda = @" AND (f.cedula ILIKE @Buscar 
                                 OR f.nombre_completo ILIKE @Buscar 
                                 OR f.email ILIKE @Buscar)";
        }

        var sqlCount = $@"
            SELECT COUNT(*)
            FROM funcionarios f
            WHERE 1=1 {filtroInst} {filtroBusqueda};";

        var sqlData = $@"
            {BaseSelect}
            WHERE 1=1 {filtroInst} {filtroBusqueda}
            ORDER BY f.nombre_completo
            LIMIT @Tamano OFFSET @Offset;";

        using var cn = new NpgsqlConnection(_cs);

        var parametros = new
        {
            InstitucionId = _sesion.InstitucionId,
            Buscar = $"%{filtroTexto}%",
            Tamano = tamano,
            Offset = (pagina - 1) * tamano
        };

        var total = await cn.ExecuteScalarAsync<int>(sqlCount, parametros);
        var items = (await cn.QueryAsync<FuncionarioDTO>(sqlData, parametros)).ToList();

        return new ResultadoPaginado<FuncionarioDTO>
        {
            Items = items,
            TotalRegistros = total,
            PaginaActual = pagina,
            TamanoPagina = tamano
        };
    }

    public async Task<ResultadoPaginado<FuncionarioCuentandanteDTO>> ObtenerCuentandantesPaginadoAsync(
        int pagina = 1,
        int tamano = 25,
        string? filtroTexto = null)
    {
        if (pagina < 1) pagina = 1;
        if (tamano < 1) tamano = 25;
        if (tamano > 200) tamano = 200;

        var (filtroInst, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "f");

        var filtroBusqueda = string.Empty;
        if (!string.IsNullOrWhiteSpace(filtroTexto))
        {
            filtroBusqueda = @" AND (f.cedula ILIKE @Buscar 
                                 OR f.nombre_completo ILIKE @Buscar)";
        }

        var sqlCount = $@"
            SELECT COUNT(DISTINCT f.id)
            FROM funcionarios f
            INNER JOIN bienes b ON b.funcionario_id = f.id AND b.activo = TRUE
            WHERE 1=1 {filtroInst} {filtroBusqueda};";

        var sqlData = $@"
            SELECT
                f.id,
                f.cedula,
                f.nombre_completo AS NombreCompleto,
                f.cargo,
                f.tipo_vinculacion AS TipoVinculacion,
                s.nombre AS SedeNombre,
                COUNT(b.id) AS BienesAsignados,
                COALESCE(SUM(b.valor_adquisicion), 0) AS ValorTotalBienes
            FROM funcionarios f
            INNER JOIN bienes b ON b.funcionario_id = f.id AND b.activo = TRUE
            LEFT JOIN sedes s ON s.id = f.sede_id
            WHERE 1=1 {filtroInst} {filtroBusqueda}
            GROUP BY f.id, f.cedula, f.nombre_completo, f.cargo, f.tipo_vinculacion, s.nombre
            ORDER BY f.nombre_completo
            LIMIT @Tamano OFFSET @Offset;";

        using var cn = new NpgsqlConnection(_cs);

        var parametros = new
        {
            InstitucionId = _sesion.InstitucionId,
            Buscar = $"%{filtroTexto}%",
            Tamano = tamano,
            Offset = (pagina - 1) * tamano
        };

        var total = await cn.ExecuteScalarAsync<int>(sqlCount, parametros);
        var items = (await cn.QueryAsync<FuncionarioCuentandanteDTO>(sqlData, parametros)).ToList();

        return new ResultadoPaginado<FuncionarioCuentandanteDTO>
        {
            Items = items,
            TotalRegistros = total,
            PaginaActual = pagina,
            TamanoPagina = tamano
        };
    }
}