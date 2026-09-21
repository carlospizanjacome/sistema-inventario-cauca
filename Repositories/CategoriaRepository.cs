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

        public async Task<IEnumerable<Categoria>> ObtenerTodosAsync()
        {
            const string sql = @"
                SELECT
                    id,
                    nombre,
                    descripcion,
                    estado,
                    fecha_creacion AS FechaCreacion
                FROM categorias
                ORDER BY nombre;
            ";

            using var connection = Connection;

            return await connection.QueryAsync<Categoria>(sql);
        }

        public async Task<Categoria?> ObtenerPorIdAsync(int id)
        {
            const string sql = @"
                SELECT
                    id,
                    nombre,
                    descripcion,
                    estado,
                    fecha_creacion AS FechaCreacion
                FROM categorias
                WHERE id = @Id;
            ";

            using var connection = Connection;

            return await connection.QueryFirstOrDefaultAsync<Categoria>(
                sql,
                new { Id = id });
        }

        public async Task<int> CrearAsync(Categoria categoria)
        {
            const string sql = @"
                INSERT INTO categorias
                (
                    nombre,
                    descripcion,
                    estado
                )
                VALUES
                (
                    @Nombre,
                    @Descripcion,
                    @Estado
                )
                RETURNING id;
            ";

            using var connection = Connection;

            return await connection.ExecuteScalarAsync<int>(
                sql,
                categoria);
        }

        public async Task<bool> ActualizarAsync(Categoria categoria)
        {
            const string sql = @"
                UPDATE categorias
                SET
                    nombre = @Nombre,
                    descripcion = @Descripcion,
                    estado = @Estado
                WHERE id = @Id;
            ";

            using var connection = Connection;

            var filas = await connection.ExecuteAsync(
                sql,
                categoria);

            return filas > 0;
        }

        public async Task<bool> EliminarAsync(int id)
        {
            const string sql = @"
                DELETE FROM categorias
                WHERE id = @Id;
            ";

            using var connection = Connection;

            var filas = await connection.ExecuteAsync(
                sql,
                new { Id = id });

            return filas > 0;
        }
    }
}