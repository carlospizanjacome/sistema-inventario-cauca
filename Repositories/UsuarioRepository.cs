using Almacen.Interfaces;
using Almacen.Models;
using Dapper;
using Npgsql;

namespace Almacen.Repositories
{
    public class UsuarioRepository : IUsuarioRepository
    {
        private readonly IConfiguration _configuration;

        public UsuarioRepository(IConfiguration configuration)
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

        public async Task<IEnumerable<Usuario>> ObtenerTodosAsync()
        {
            const string sql = @"
                SELECT
                    u.id,
                    u.nombre_completo  AS NombreCompleto,
                    u.email,
                    u.password_hash    AS PasswordHash,
                    u.activo,
                    u.rol_id           AS RolId,
                    r.nombre           AS NombreRol,
                    u.institucion_id   AS InstitucionId,
                    u.es_super_admin   AS EsSuperAdmin,
                    i.nombre           AS InstitucionNombre
                FROM usuarios u
                LEFT JOIN roles r         ON r.id = u.rol_id
                LEFT JOIN instituciones i ON i.id = u.institucion_id
                ORDER BY u.nombre_completo;
            ";

            using var connection = Connection;
            return await connection.QueryAsync<Usuario>(sql);
        }

        public async Task<IEnumerable<Usuario>> ObtenerPorInstitucionAsync(int institucionId)
        {
            const string sql = @"
                SELECT
                    u.id,
                    u.nombre_completo  AS NombreCompleto,
                    u.email,
                    u.password_hash    AS PasswordHash,
                    u.activo,
                    u.rol_id           AS RolId,
                    r.nombre           AS NombreRol,
                    u.institucion_id   AS InstitucionId,
                    u.es_super_admin   AS EsSuperAdmin,
                    i.nombre           AS InstitucionNombre
                FROM usuarios u
                LEFT JOIN roles r         ON r.id = u.rol_id
                LEFT JOIN instituciones i ON i.id = u.institucion_id
                WHERE u.institucion_id = @InstitucionId
                ORDER BY u.nombre_completo;
            ";

            using var connection = Connection;
            return await connection.QueryAsync<Usuario>(sql, new { InstitucionId = institucionId });
        }

        public async Task<Usuario?> ObtenerPorIdAsync(int id)
        {
            const string sql = @"
                SELECT
                    u.id,
                    u.nombre_completo  AS NombreCompleto,
                    u.email,
                    u.password_hash    AS PasswordHash,
                    u.activo,
                    u.rol_id           AS RolId,
                    r.nombre           AS NombreRol,
                    u.institucion_id   AS InstitucionId,
                    u.es_super_admin   AS EsSuperAdmin,
                    i.nombre           AS InstitucionNombre
                FROM usuarios u
                LEFT JOIN roles r         ON r.id = u.rol_id
                LEFT JOIN instituciones i ON i.id = u.institucion_id
                WHERE u.id = @Id;
            ";

            using var connection = Connection;
            return await connection.QueryFirstOrDefaultAsync<Usuario>(sql, new { Id = id });
        }

        public async Task<Usuario?> ObtenerPorEmailAsync(string email)
        {
            const string sql = @"
                SELECT
                    u.id,
                    u.nombre_completo  AS NombreCompleto,
                    u.email,
                    u.password_hash    AS PasswordHash,
                    u.activo,
                    u.rol_id           AS RolId,
                    r.nombre           AS NombreRol,
                    u.institucion_id   AS InstitucionId,
                    u.es_super_admin   AS EsSuperAdmin,
                    i.nombre           AS InstitucionNombre
                FROM usuarios u
                LEFT JOIN roles r         ON r.id = u.rol_id
                LEFT JOIN instituciones i ON i.id = u.institucion_id
                WHERE u.email = @Email;
            ";

            using var connection = Connection;
            return await connection.QueryFirstOrDefaultAsync<Usuario>(sql, new { Email = email });
        }

        public async Task<int> CrearAsync(Usuario usuario)
        {
            const string sql = @"
                INSERT INTO usuarios
                (
                    nombre_completo,
                    email,
                    password_hash,
                    activo,
                    rol_id,
                    institucion_id,
                    es_super_admin
                )
                VALUES
                (
                    @NombreCompleto,
                    @Email,
                    @PasswordHash,
                    @Activo,
                    @RolId,
                    @InstitucionId,
                    @EsSuperAdmin
                )
                RETURNING id;
            ";

            using var connection = Connection;
            return await connection.ExecuteScalarAsync<int>(sql, usuario);
        }

        public async Task ActualizarAsync(Usuario usuario)
        {
            const string sql = @"
                UPDATE usuarios
                SET
                    nombre_completo = @NombreCompleto,
                    email = @Email,
                    password_hash = @PasswordHash,
                    activo = @Activo,
                    rol_id = @RolId,
                    institucion_id = @InstitucionId,
                    es_super_admin = @EsSuperAdmin
                WHERE id = @Id;
            ";

            using var connection = Connection;
            await connection.ExecuteAsync(sql, usuario);
        }

        public async Task<bool> EliminarAsync(int id)
        {
            const string sql = @"
                DELETE FROM usuarios
                WHERE id = @Id;
            ";

            using var connection = Connection;
            var filas = await connection.ExecuteAsync(sql, new { Id = id });
            return filas > 0;
        }

        public async Task<bool> ExisteEmailAsync(string email, int? excluirId = null)
        {
            const string sql = @"
                SELECT COUNT(*) FROM usuarios
                WHERE email = @Email
                  AND (@ExcluirId IS NULL OR id <> @ExcluirId);
            ";

            using var connection = Connection;
            var n = await connection.ExecuteScalarAsync<int>(sql,
                new { Email = email, ExcluirId = excluirId });
            return n > 0;
        }
    }
}