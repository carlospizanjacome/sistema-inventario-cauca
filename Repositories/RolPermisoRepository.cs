using Almacen.Interfaces;
using Almacen.Models;
using Dapper;
using Npgsql;

namespace Almacen.Repositories
{
    public class RolPermisoRepository : IRolPermisoRepository
    {
        private readonly IConfiguration _configuration;

        public RolPermisoRepository(IConfiguration configuration)
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

        public async Task<IEnumerable<RolPermiso>> ObtenerPorRolAsync(int rolId)
        {
            const string sql = @"
                SELECT
                    id,
                    rol_id AS RolId,
                    permiso_id AS PermisoId
                FROM rol_permisos
                WHERE rol_id = @RolId;
            ";

            using var connection = Connection;

            return await connection.QueryAsync<RolPermiso>(
                sql,
                new { RolId = rolId }
            );
        }

        public async Task AsignarPermisoAsync(int rolId, int permisoId)
        {
            const string sql = @"
                INSERT INTO rol_permisos (rol_id, permiso_id)
                VALUES (@RolId, @PermisoId)
                ON CONFLICT (rol_id, permiso_id) DO NOTHING;
            ";

            using var connection = Connection;

            await connection.ExecuteAsync(
                sql,
                new { RolId = rolId, PermisoId = permisoId }
            );
        }

        public async Task EliminarPermisoAsync(int rolId, int permisoId)
        {
            const string sql = @"
                DELETE FROM rol_permisos
                WHERE rol_id = @RolId
                  AND permiso_id = @PermisoId;
            ";

            using var connection = Connection;

            await connection.ExecuteAsync(
                sql,
                new { RolId = rolId, PermisoId = permisoId }
            );
        }

        /// <summary>
        /// Reemplazo atómico de permisos de un rol.
        /// 1. Abre transacción
        /// 2. Borra todos los permisos actuales del rol
        /// 3. Inserta los nuevos en una sola query con UNNEST (Postgres native)
        /// 4. Commit o Rollback según resultado
        /// </summary>
        public async Task ReemplazarPermisosAsync(int rolId, IEnumerable<int> permisoIds)
        {
            var ids = permisoIds?.Distinct().ToArray() ?? Array.Empty<int>();

            using var connection = Connection;
            await connection.OpenAsync();
            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                // 1) Borrar los actuales
                await connection.ExecuteAsync(
                    "DELETE FROM rol_permisos WHERE rol_id = @RolId;",
                    new { RolId = rolId },
                    transaction);

                // 2) Insertar los nuevos en 1 sola query
                if (ids.Length > 0)
                {
                    const string insertSql = @"
                        INSERT INTO rol_permisos (rol_id, permiso_id)
                        SELECT @RolId, unnest(@PermisoIds::int[])
                        ON CONFLICT (rol_id, permiso_id) DO NOTHING;
                    ";

                    await connection.ExecuteAsync(
                        insertSql,
                        new { RolId = rolId, PermisoIds = ids },
                        transaction);
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}