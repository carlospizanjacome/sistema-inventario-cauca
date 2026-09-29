using Almacen.Helpers;
using Almacen.Interfaces;
using Almacen.Models;
using Almacen.Services;
using Dapper;
using Npgsql;

namespace Almacen.Repositories
{
    public class CategoriaRepository : ICategoriaRepository
    {
        private readonly IConfiguration _configuration;
        private readonly UsuarioSesionService _sesion;

        public CategoriaRepository(IConfiguration configuration, UsuarioSesionService sesion)
        {
            _configuration = configuration;
            _sesion = sesion;
        }

        private NpgsqlConnection Connection
        {
            get
            {
                return new NpgsqlConnection(
                    _configuration.GetConnectionString("DefaultConnection"));
            }
        }

        private const string BaseSelect = @"
            SELECT
                c.id,
                c.nombre,
                c.descripcion,
                c.codigo_cgn AS CodigoCgn,
                c.tipo_bien AS TipoBien,
                c.estado,
                c.fecha_creacion AS FechaCreacion,
                (SELECT COUNT(*)::int FROM bienes b
                 WHERE b.categoria_id = c.id 
                   AND b.activo = TRUE
                   AND b.institucion_id = @InstitucionId) AS TotalBienes
            FROM categorias c";

        public async Task<IEnumerable<Categoria>> ObtenerTodosAsync()
        {
            var sql = $"{BaseSelect} ORDER BY c.nombre;";
            using var connection = Connection;
            return await connection.QueryAsync<Categoria>(sql,
                new { InstitucionId = _sesion.InstitucionId });
        }

        public async Task<IEnumerable<Categoria>> ObtenerPorTipoBienAsync(string tipoBien)
        {
            var sql = $@"{BaseSelect} 
                WHERE c.estado = TRUE
                  AND c.tipo_bien IN (@Tipo, 'ambos')
                ORDER BY c.nombre;";

            using var connection = Connection;
            return await connection.QueryAsync<Categoria>(sql, new
            {
                Tipo = tipoBien,
                InstitucionId = _sesion.InstitucionId
            });
        }

        public async Task<ResultadoPaginado<Categoria>> ObtenerPaginadoAsync(
            int pagina = 1,
            int tamano = 25,
            string? filtroTexto = null,
            bool? estado = null,
            string? tipoBien = null)
        {
            if (pagina < 1) pagina = 1;
            if (tamano < 1) tamano = 25;
            if (tamano > 200) tamano = 200;

            var condiciones = new List<string>();
            var parametros = new DynamicParameters();
            parametros.Add("InstitucionId", _sesion.InstitucionId);

            if (!string.IsNullOrWhiteSpace(filtroTexto))
            {
                condiciones.Add("(c.nombre ILIKE @Buscar OR c.descripcion ILIKE @Buscar OR c.codigo_cgn ILIKE @Buscar)");
                parametros.Add("Buscar", $"%{filtroTexto}%");
            }

            if (estado.HasValue)
            {
                condiciones.Add("c.estado = @Estado");
                parametros.Add("Estado", estado.Value);
            }

            if (!string.IsNullOrWhiteSpace(tipoBien))
            {
                condiciones.Add("c.tipo_bien = @TipoBien");
                parametros.Add("TipoBien", tipoBien);
            }

            var whereExtra = condiciones.Any()
                ? " WHERE " + string.Join(" AND ", condiciones)
                : string.Empty;

            parametros.Add("Tamano", tamano);
            parametros.Add("Offset", (pagina - 1) * tamano);

            var sqlCount = $"SELECT COUNT(*) FROM categorias c {whereExtra};";
            var sqlData = $"{BaseSelect} {whereExtra} ORDER BY c.nombre LIMIT @Tamano OFFSET @Offset;";

            using var connection = Connection;

            var total = await connection.ExecuteScalarAsync<int>(sqlCount, parametros);
            var items = (await connection.QueryAsync<Categoria>(sqlData, parametros)).ToList();

            return new ResultadoPaginado<Categoria>
            {
                Items = items,
                TotalRegistros = total,
                PaginaActual = pagina,
                TamanoPagina = tamano
            };
        }

        public async Task<Categoria?> ObtenerPorIdAsync(int id)
        {
            var sql = $"{BaseSelect} WHERE c.id = @Id;";
            using var connection = Connection;
            return await connection.QueryFirstOrDefaultAsync<Categoria>(sql, new
            {
                Id = id,
                InstitucionId = _sesion.InstitucionId
            });
        }

        public async Task<int> CrearAsync(Categoria categoria)
        {
            const string sql = @"
                INSERT INTO categorias (nombre, descripcion, codigo_cgn, tipo_bien, estado)
                VALUES (@Nombre, @Descripcion, @CodigoCgn, @TipoBien, @Estado)
                RETURNING id;";
            using var connection = Connection;
            return await connection.ExecuteScalarAsync<int>(sql, categoria);
        }

        public async Task<bool> ActualizarAsync(Categoria categoria)
        {
            const string sql = @"
                UPDATE categorias
                SET nombre = @Nombre,
                    descripcion = @Descripcion,
                    codigo_cgn = @CodigoCgn,
                    tipo_bien = @TipoBien,
                    estado = @Estado
                WHERE id = @Id;";
            using var connection = Connection;
            return await connection.ExecuteAsync(sql, categoria) > 0;
        }

        public async Task<bool> EliminarAsync(int id)
        {
            const string sql = @"DELETE FROM categorias WHERE id = @Id;";
            using var connection = Connection;
            return await connection.ExecuteAsync(sql, new { Id = id }) > 0;
        }
    }
}