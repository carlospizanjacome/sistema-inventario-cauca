using Almacen.DTOs;
using Almacen.Helpers;
using Almacen.Interfaces;
using Almacen.Models;
using Almacen.Services;
using Dapper;
using Npgsql;

namespace Almacen.Repositories
{
    public class BienRepository : IBienRepository
    {
        private readonly IConfiguration _configuration;
        private readonly UsuarioSesionService _sesion;

        public BienRepository(IConfiguration configuration, UsuarioSesionService sesion)
        {
            _configuration = configuration;
            _sesion = sesion;
        }

        private NpgsqlConnection Connection
            => new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));

        private const string BaseSelect = @"
            SELECT
                b.id,
                b.codigo,
                b.nombre,
                b.descripcion,
                b.categoria_id AS CategoriaId,
                b.tipo_bien AS TipoBien,
                b.marca,
                b.modelo,
                b.serie,
                b.valor_adquisicion AS ValorAdquisicion,
                b.fecha_adquisicion::timestamp AS FechaAdquisicion,
                b.estado_fisico AS EstadoFisico,
                b.ubicacion,
                b.responsable,
                b.activo,
                b.created_at AS CreatedAt,
                b.updated_at AS UpdatedAt,
                b.institucion_id AS InstitucionId,
                b.aula_id AS AulaId,
                b.funcionario_id AS FuncionarioId,
                b.vida_util_id AS VidaUtilId,
                b.codigo_qr AS CodigoQr,
                b.valor_residual AS ValorResidual,
                b.cantidad,
                b.depreciacion_acumulada AS DepreciacionAcumulada,
                b.valor_neto AS ValorNeto,
                c.nombre  AS CategoriaNombre,
                a.nombre  AS AulaNombre,
                bl.nombre AS BloqueNombre,
                s.nombre  AS SedeNombre,
                f.nombre_completo AS FuncionarioNombre
            FROM bienes b
            LEFT JOIN categorias c   ON c.id = b.categoria_id
            LEFT JOIN aulas a        ON a.id = b.aula_id
            LEFT JOIN bloques bl     ON bl.id = a.bloque_id
            LEFT JOIN sedes s        ON s.id = bl.sede_id
            LEFT JOIN funcionarios f ON f.id = b.funcionario_id";

        public async Task<IEnumerable<Bien>> ObtenerTodosAsync()
        {
            var (filtro, param) = FiltroInstitucion.Construir(
                _sesion.EsSuperAdmin,
                _sesion.InstitucionId,
                tabla: "b");

            var sql = $"{BaseSelect} WHERE 1=1 {filtro} ORDER BY b.nombre;";

            using var connection = Connection;
            return await connection.QueryAsync<Bien>(sql, param);
        }

        public async Task<Bien?> ObtenerPorIdAsync(int id)
        {
            var (filtro, param) = FiltroInstitucion.Construir(
                _sesion.EsSuperAdmin,
                _sesion.InstitucionId,
                tabla: "b");

            var sql = $"{BaseSelect} WHERE b.id = @Id {filtro};";

            using var connection = Connection;
            return await connection.QueryFirstOrDefaultAsync<Bien>(sql,
                new { Id = id, InstitucionId = _sesion.InstitucionId });
        }

        public async Task<int> CrearAsync(Bien bien)
        {
            // Asignar institución del usuario actual
            bien.InstitucionId = _sesion.InstitucionId;

            const string sql = @"
                INSERT INTO bienes
                (
                    codigo, nombre, descripcion, categoria_id, tipo_bien,
                    marca, modelo, serie, valor_adquisicion, fecha_adquisicion,
                    estado_fisico, ubicacion, responsable, activo,
                    institucion_id, aula_id, funcionario_id, vida_util_id,
                    codigo_qr, valor_residual, cantidad,
                    created_at, updated_at
                )
                VALUES
                (
                    @Codigo, @Nombre, @Descripcion, @CategoriaId, @TipoBien,
                    @Marca, @Modelo, @Serie, @ValorAdquisicion, @FechaAdquisicion,
                    @EstadoFisico, @Ubicacion, @Responsable, @Activo,
                    @InstitucionId, @AulaId, @FuncionarioId, @VidaUtilId,
                    @CodigoQr, @ValorResidual, @Cantidad,
                    NOW(), NOW()
                )
                RETURNING id;";

            using var connection = Connection;
            return await connection.ExecuteScalarAsync<int>(sql, bien);
        }

        public async Task ActualizarAsync(Bien bien)
        {
            const string sql = @"
                UPDATE bienes
                SET
                    codigo = @Codigo,
                    nombre = @Nombre,
                    descripcion = @Descripcion,
                    categoria_id = @CategoriaId,
                    tipo_bien = @TipoBien,
                    marca = @Marca,
                    modelo = @Modelo,
                    serie = @Serie,
                    valor_adquisicion = @ValorAdquisicion,
                    fecha_adquisicion = @FechaAdquisicion,
                    estado_fisico = @EstadoFisico,
                    ubicacion = @Ubicacion,
                    responsable = @Responsable,
                    activo = @Activo,
                    aula_id = @AulaId,
                    funcionario_id = @FuncionarioId,
                    vida_util_id = @VidaUtilId,
                    codigo_qr = @CodigoQr,
                    valor_residual = @ValorResidual,
                    cantidad = @Cantidad,
                    updated_at = NOW()
                WHERE id = @Id;";

            using var connection = Connection;
            await connection.ExecuteAsync(sql, bien);
        }

        public async Task<bool> EliminarAsync(int id)
        {
            using var connection = Connection;
            var filas = await connection.ExecuteAsync(
                "DELETE FROM bienes WHERE id = @Id;", new { Id = id });
            return filas > 0;
        }

        public async Task<ResultadoPaginado<Bien>> ObtenerPaginadoAsync(
            int pagina = 1,
            int tamano = 25,
            string? filtroTexto = null)
        {
            // Normalizar parámetros
            if (pagina < 1) pagina = 1;
            if (tamano < 1) tamano = 25;
            if (tamano > 200) tamano = 200;

            var (filtroInst, _) = FiltroInstitucion.Construir(
                _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "b");

            // Filtro de búsqueda (opcional)
            var filtroBusqueda = string.Empty;
            if (!string.IsNullOrWhiteSpace(filtroTexto))
            {
                filtroBusqueda = " AND (b.codigo ILIKE @Buscar OR b.nombre ILIKE @Buscar)";
            }

            var sqlCount = $@"
                SELECT COUNT(*)
                FROM bienes b
                WHERE 1=1 {filtroInst} {filtroBusqueda};";

            var sqlData = $@"
                {BaseSelect}
                WHERE 1=1 {filtroInst} {filtroBusqueda}
                ORDER BY b.nombre
                LIMIT @Tamano OFFSET @Offset;";

            using var connection = Connection;

            var parametros = new
            {
                InstitucionId = _sesion.InstitucionId,
                Buscar = $"%{filtroTexto}%",
                Tamano = tamano,
                Offset = (pagina - 1) * tamano
            };

            var total = await connection.ExecuteScalarAsync<int>(sqlCount, parametros);
            var items = (await connection.QueryAsync<Bien>(sqlData, parametros)).ToList();

            return new ResultadoPaginado<Bien>
            {
                Items = items,
                TotalRegistros = total,
                PaginaActual = pagina,
                TamanoPagina = tamano
            };

        }
        public async Task<ResultadoPaginado<Bien>> ObtenerPaginadoConFiltrosAsync(
    FiltroBienDTO filtro,
    int pagina = 1,
    int tamano = 25)
        {
            if (pagina < 1) pagina = 1;
            if (tamano < 1) tamano = 25;
            if (tamano > 200) tamano = 200;

            var (filtroInst, _) = FiltroInstitucion.Construir(
                _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "b");

            var condiciones = new List<string>();
            var parametros = new DynamicParameters();
            parametros.Add("InstitucionId", _sesion.InstitucionId);

            if (!string.IsNullOrWhiteSpace(filtro.Texto))
            {
                condiciones.Add("(b.codigo ILIKE @Texto OR b.nombre ILIKE @Texto OR b.serie ILIKE @Texto)");
                parametros.Add("Texto", $"%{filtro.Texto}%");
            }

            if (filtro.CategoriaId.HasValue)
            {
                condiciones.Add("b.categoria_id = @CategoriaId");
                parametros.Add("CategoriaId", filtro.CategoriaId.Value);
            }

            if (filtro.SedeId.HasValue)
            {
                condiciones.Add("s.id = @SedeId");
                parametros.Add("SedeId", filtro.SedeId.Value);
            }

            if (filtro.AulaId.HasValue)
            {
                condiciones.Add("b.aula_id = @AulaId");
                parametros.Add("AulaId", filtro.AulaId.Value);
            }

            if (filtro.FuncionarioId.HasValue)
            {
                condiciones.Add("b.funcionario_id = @FuncionarioId");
                parametros.Add("FuncionarioId", filtro.FuncionarioId.Value);
            }

            if (!string.IsNullOrWhiteSpace(filtro.EstadoFisico))
            {
                condiciones.Add("b.estado_fisico = @EstadoFisico");
                parametros.Add("EstadoFisico", filtro.EstadoFisico);
            }

            if (!string.IsNullOrWhiteSpace(filtro.TipoBien))
            {
                condiciones.Add("b.tipo_bien = @TipoBien");
                parametros.Add("TipoBien", filtro.TipoBien);
            }

            if (filtro.Activo.HasValue)
            {
                condiciones.Add("b.activo = @Activo");
                parametros.Add("Activo", filtro.Activo.Value);
            }

            if (filtro.FechaDesde.HasValue)
            {
                condiciones.Add("b.fecha_adquisicion >= @FechaDesde");
                parametros.Add("FechaDesde", filtro.FechaDesde.Value);
            }

            if (filtro.FechaHasta.HasValue)
            {
                condiciones.Add("b.fecha_adquisicion <= @FechaHasta");
                parametros.Add("FechaHasta", filtro.FechaHasta.Value);
            }

            var whereExtra = condiciones.Any()
                ? " AND " + string.Join(" AND ", condiciones)
                : string.Empty;

            parametros.Add("Tamano", tamano);
            parametros.Add("Offset", (pagina - 1) * tamano);

            var sqlCount = $@"
        SELECT COUNT(*)
        FROM bienes b
        LEFT JOIN aulas a ON a.id = b.aula_id
        LEFT JOIN bloques bl ON bl.id = a.bloque_id
        LEFT JOIN sedes s ON s.id = bl.sede_id
        WHERE 1=1 {filtroInst} {whereExtra};";

            var sqlData = $@"
        {BaseSelect}
        WHERE 1=1 {filtroInst} {whereExtra}
        ORDER BY b.nombre
        LIMIT @Tamano OFFSET @Offset;";

            using var connection = Connection;

            var total = await connection.ExecuteScalarAsync<int>(sqlCount, parametros);
            var items = (await connection.QueryAsync<Bien>(sqlData, parametros)).ToList();

            return new ResultadoPaginado<Bien>
            {
                Items = items,
                TotalRegistros = total,
                PaginaActual = pagina,
                TamanoPagina = tamano
            };
        }

        public async Task<IEnumerable<Bien>> ObtenerPorFuncionarioAsync(int funcionarioId)
        {
            var (filtroInst, _) = FiltroInstitucion.Construir(
                _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "b");

            var sql = $@"
        {BaseSelect}
        WHERE b.funcionario_id = @FuncionarioId
          AND b.activo = TRUE
          {filtroInst}
        ORDER BY b.nombre;";

            using var connection = Connection;
            return await connection.QueryAsync<Bien>(sql,
                new { FuncionarioId = funcionarioId, InstitucionId = _sesion.InstitucionId });
        }
    }
}