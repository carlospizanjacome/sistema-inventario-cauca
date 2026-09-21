using Almacen.Interfaces;
using Almacen.Models;
using Dapper;
using Npgsql;

namespace Almacen.Repositories
{
    public class RolRepository : IRolRepository
    {
        private readonly IConfiguration _configuration;

        public RolRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private NpgsqlConnection Connection
        {
            get
            {
                return new NpgsqlConnection(
                    _configuration.GetConnectionString("DefaultConnection")
                );
            }
        }

        public async Task<IEnumerable<Rol>> ObtenerTodosAsync()
        {
            const string sql = @"
                SELECT
                    id,
                    nombre,
                    descripcion,
                    activo,
                    fecha_creacion
                FROM roles
                ORDER BY nombre;
            ";

            using var connection = Connection;

            return await connection.QueryAsync<Rol>(sql);
        }

        public async Task<Rol?> ObtenerPorIdAsync(int id)
        {
            const string sql = @"
                SELECT
                    id,
                    nombre,
                    descripcion,
                    activo,
                    fecha_creacion
                FROM roles
                WHERE id = @Id;
            ";

            using var connection = Connection;

            return await connection.QueryFirstOrDefaultAsync<Rol>(
                sql,
                new { Id = id }
            );
        }

        public async Task<int> CrearAsync(Rol rol)
        {
            const string sql = @"
                INSERT INTO roles
                (
                    nombre,
                    descripcion,
                    activo
                )
                VALUES
                (
                    @Nombre,
                    @Descripcion,
                    @Activo
                )
                RETURNING id;
            ";

            using var connection = Connection;

            return await connection.ExecuteScalarAsync<int>(
                sql,
                rol
            );
        }

        public async Task ActualizarAsync(Rol rol)
        {
            const string sql = @"
                UPDATE roles
                SET
                    nombre = @Nombre,
                    descripcion = @Descripcion,
                    activo = @Activo
                WHERE id = @Id;
            ";

            using var connection = Connection;

            await connection.ExecuteAsync(
                sql,
                rol
            );
        }

        public async Task<bool> EliminarAsync(int id)
        {
            const string sql = @"
                DELETE FROM roles
                WHERE id = @Id;
            ";

            using var connection = Connection;

            var filas = await connection.ExecuteAsync(
                sql,
                new { Id = id }
            );

            return filas > 0;
        }
    }
}