using Almacen.Interfaces;
using Almacen.Models;
using Dapper;
using Npgsql;

namespace Almacen.Repositories
{
    public class PermisoRepository : IPermisoRepository
    {
        private readonly IConfiguration _configuration;

        public PermisoRepository(IConfiguration configuration)
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

        public async Task<IEnumerable<Permiso>> ObtenerTodosAsync()
        {
            const string sql = @"
                SELECT
                    id,
                    codigo,
                    descripcion,
                    modulo,
                    activo
                FROM permisos
                ORDER BY modulo, codigo;
            ";

            using var connection = Connection;

            return await connection.QueryAsync<Permiso>(sql);
        }

        public async Task<Permiso?> ObtenerPorIdAsync(int id)
        {
            const string sql = @"
                SELECT
                    id,
                    codigo,
                    descripcion,
                    modulo,
                    activo
                FROM permisos
                WHERE id = @Id;
            ";

            using var connection = Connection;

            return await connection.QueryFirstOrDefaultAsync<Permiso>(
                sql,
                new { Id = id }
            );
        }

        public async Task<int> CrearAsync(Permiso permiso)
        {
            const string sql = @"
                INSERT INTO permisos
                (
                    codigo,
                    descripcion,
                    modulo,
                    activo
                )
                VALUES
                (
                    @Codigo,
                    @Descripcion,
                    @Modulo,
                    @Activo
                )
                RETURNING id;
            ";

            using var connection = Connection;

            return await connection.ExecuteScalarAsync<int>(
                sql,
                permiso
            );
        }

        public async Task ActualizarAsync(Permiso permiso)
        {
            const string sql = @"
                UPDATE permisos
                SET
                    codigo = @Codigo,
                    descripcion = @Descripcion,
                    modulo = @Modulo,
                    activo = @Activo
                WHERE id = @Id;
            ";

            using var connection = Connection;

            await connection.ExecuteAsync(
                sql,
                permiso
            );
        }

        public async Task<bool> EliminarAsync(int id)
        {
            const string sql = @"
                DELETE FROM permisos
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