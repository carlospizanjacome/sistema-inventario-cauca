using Almacen.Helpers;
using Almacen.Interfaces;
using Almacen.Models;
using Dapper;
using Npgsql;

namespace Almacen.Repositories
{
    public class CategoriaRepository : ICategoriaRepository
    {
        private readonly IConfiguration _configuration;

        public CategoriaRepository(IConfiguration configuration)
        {
            _configuration = configuration;
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
                id,
                nombre,
                descripcion,
                codigo_cgn AS CodigoCgn,
                estado,
                fecha_creacion AS FechaCreacion
            FROM categorias";

        public async Task<IEnumerable<Categoria>> ObtenerTodosAsync()
        {
            var sql = $"{BaseSelect} ORDER BY nombre;";
            using var connection = Connection;
            return await connection.QueryAsync<Categoria>(sql);
        }

        public async Task<ResultadoPaginado<Categoria>> ObtenerPaginadoAsync(
            int pagina = 1,
            int tamano = 25,
            string? filtroTexto = null,
            bool? estado = null)
        {
            if (pagina < 1) pagina = 1;
            if (tamano < 1) tamano = 25;
            if (tamano > 200) tamano = 200;

            var condiciones = new List<string>();
            var parametros = new DynamicParameters();

            if (!string.IsNullOrWhiteSpace(filtroTexto))
            {
                condiciones.Add("(nombre ILIKE @Buscar OR descripcion ILIKE @Buscar OR codigo_cgn ILIKE @Buscar)");
                parametros.Add("Buscar", $"%{filtroTexto}%");
            }

            if (estado.HasValue)
            {
                condiciones.Add("estado = @Estado");
                parametros.Add("Estado", estado.Value);
            }

            var whereExtra = condiciones.Any()
                ? " WHERE " + string.Join(" AND ", condiciones)
                : string.Empty;

            parametros.Add("Tamano", tamano);
            parametros.Add("Offset", (pagina - 1) * tamano);

            var sqlCount = $"SELECT COUNT(*) FROM categorias {whereExtra};";
            var sqlData = $"{BaseSelect} {whereExtra} ORDER BY nombre LIMIT @Tamano OFFSET @Offset;";

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
            var sql = $"{BaseSelect} WHERE id = @Id;";
            using var connection = Connection;
            return await connection.QueryFirstOrDefaultAsync<Categoria>(sql, new { Id = id });
        }

        public async Task<int> CrearAsync(Categoria categoria)
        {
            const string sql = @"
                INSERT INTO categorias (nombre, descripcion, codigo_cgn, estado)
                VALUES (@Nombre, @Descripcion, @CodigoCgn, @Estado)
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