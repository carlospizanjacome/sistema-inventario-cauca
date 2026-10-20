using Almacen.Interfaces;
using Almacen.Models;
using Almacen.Services;
using Dapper;
using Npgsql;
using BCrypt.Net;

namespace Almacen.Repositories
{
    public class UsuarioRepository : IUsuarioRepository
    {
        private readonly IConfiguration _configuration;
        private readonly UsuarioSesionService _sesion;

        public UsuarioRepository(IConfiguration configuration, UsuarioSesionService sesion)
        {
            _configuration = configuration;
            _sesion = sesion;
        }

        private NpgsqlConnection Connection =>
            new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));

        private const string SelectBase = @"
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
                i.nombre           AS InstitucionNombre,
                u.sede_id          AS SedeId,
                s.nombre           AS SedeNombre
            FROM usuarios u
            LEFT JOIN roles r         ON r.id = u.rol_id
            LEFT JOIN instituciones i ON i.id = u.institucion_id
            LEFT JOIN sedes s         ON s.id = u.sede_id";

        private bool DebeFiltrarPorInstitucion()
            => !_sesion.EsSuperAdmin && _sesion.InstitucionId > 0;

        private static void ValidarSedeObligatoria(Usuario usuario)
        {
            if (!usuario.EsSuperAdmin && usuario.SedeId == null)
            {
                throw new InvalidOperationException("La sede es obligatoria. Seleccione una sede para el usuario.");
            }
        }

        public async Task<IEnumerable<Usuario>> ObtenerTodosAsync()
        {
            string sql;
            object? parametros = null;

            if (DebeFiltrarPorInstitucion())
            {
                sql = $"{SelectBase} WHERE u.institucion_id = @InstitucionId ORDER BY u.nombre_completo";
                parametros = new { InstitucionId = _sesion.InstitucionId };
            }
            else
            {
                sql = $"{SelectBase} ORDER BY u.nombre_completo";
            }

            using var connection = Connection;
            return await connection.QueryAsync<Usuario>(sql, parametros);
        }

        public async Task<IEnumerable<Usuario>> ObtenerPorInstitucionAsync(int institucionId)
        {
            var sql = $"{SelectBase} WHERE u.institucion_id = @InstitucionId ORDER BY u.nombre_completo";
            using var connection = Connection;
            return await connection.QueryAsync<Usuario>(sql, new { InstitucionId = institucionId });
        }

        public async Task<Usuario?> ObtenerPorIdAsync(int id)
        {
            string sql;
            object parametros;

            if (DebeFiltrarPorInstitucion())
            {
                sql = $"{SelectBase} WHERE u.id = @Id AND u.institucion_id = @InstitucionId";
                parametros = new { Id = id, InstitucionId = _sesion.InstitucionId };
            }
            else
            {
                sql = $"{SelectBase} WHERE u.id = @Id";
                parametros = new { Id = id };
            }

            using var connection = Connection;
            return await connection.QueryFirstOrDefaultAsync<Usuario>(sql, parametros);
        }

        public async Task<Usuario?> ObtenerPorEmailAsync(string email)
        {
            var sql = $"{SelectBase} WHERE u.email = @Email";
            using var connection = Connection;
            return await connection.QueryFirstOrDefaultAsync<Usuario>(sql, new { Email = email });
        }

        public async Task<int> CrearAsync(Usuario usuario)
        {
            if (!_sesion.EsSuperAdmin)
            {
                usuario.InstitucionId = _sesion.InstitucionId;
                usuario.EsSuperAdmin = false;
            }

            ValidarSedeObligatoria(usuario);

            // 1. ENCRIPTAR LA CONTRASEÑA ANTES DE GUARDAR
            if (!string.IsNullOrWhiteSpace(usuario.PasswordHash) && !usuario.PasswordHash.StartsWith("$2"))
            {
                usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(usuario.PasswordHash);
            }

            const string sql = @"
                INSERT INTO usuarios
                (nombre_completo, email, password_hash, activo, rol_id, institucion_id, es_super_admin, sede_id)
                VALUES (@NombreCompleto, @Email, @PasswordHash, @Activo, @RolId, @InstitucionId, @EsSuperAdmin, @SedeId)
                RETURNING id;";

            const string sqlCheckMovil = "SELECT COUNT(*) FROM mobile.usuarios_mobile WHERE email = @Email";
            const string sqlInsertMovil = @"
                INSERT INTO mobile.usuarios_mobile 
                (email, password_hash, nombre_completo, institucion_id, sede_id, rol_id, activo, created_at, updated_at)
                VALUES (@Email, @PasswordHash, @NombreCompleto, @InstitucionId, @SedeId, @RolId, @Activo, NOW(), NOW());";
            const string sqlUpdateMovil = @"
                UPDATE mobile.usuarios_mobile 
                SET password_hash = @PasswordHash, nombre_completo = @NombreCompleto,
                    institucion_id = @InstitucionId, sede_id = @SedeId, rol_id = @RolId, activo = @Activo, updated_at = NOW()
                WHERE email = @Email;";

            using var connection = Connection;
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            
            try
            {
                var nuevoId = await connection.ExecuteScalarAsync<int>(sql, usuario, transaction);
                
                if (!usuario.EsSuperAdmin)
                {
                    var existe = await connection.ExecuteScalarAsync<int>(sqlCheckMovil, usuario, transaction);
                    if (existe > 0)
                        await connection.ExecuteAsync(sqlUpdateMovil, usuario, transaction);
                    else
                        await connection.ExecuteAsync(sqlInsertMovil, usuario, transaction);
                }

                transaction.Commit();
                return nuevoId;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task ActualizarAsync(Usuario usuario)
        {
            if (!_sesion.EsSuperAdmin)
            {
                usuario.InstitucionId = _sesion.InstitucionId;
                usuario.EsSuperAdmin = false;
            }

            ValidarSedeObligatoria(usuario);

            // 1. ENCRIPTAR LA CONTRASEÑA SI SE ESCRIBIÓ UNA NUEVA
            if (!string.IsNullOrWhiteSpace(usuario.PasswordHash) && !usuario.PasswordHash.StartsWith("$2"))
            {
                usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(usuario.PasswordHash);
            }

            const string sql = @"
                UPDATE usuarios
                SET nombre_completo = @NombreCompleto, email = @Email, password_hash = @PasswordHash,
                    activo = @Activo, rol_id = @RolId, institucion_id = @InstitucionId, es_super_admin = @EsSuperAdmin, sede_id = @SedeId
                WHERE id = @Id AND (@EsSuperAdminUser = TRUE OR institucion_id = @InstitucionId);";

            const string sqlCheckMovil = "SELECT COUNT(*) FROM mobile.usuarios_mobile WHERE email = @Email";
            const string sqlUpdateMovil = @"
                UPDATE mobile.usuarios_mobile 
                SET password_hash = @PasswordHash, nombre_completo = @NombreCompleto,
                    institucion_id = @InstitucionId, sede_id = @SedeId, rol_id = @RolId, activo = @Activo, updated_at = NOW()
                WHERE email = @Email;";
            const string sqlInsertMovil = @"
                INSERT INTO mobile.usuarios_mobile 
                (email, password_hash, nombre_completo, institucion_id, sede_id, rol_id, activo, created_at, updated_at)
                VALUES (@Email, @PasswordHash, @NombreCompleto, @InstitucionId, @SedeId, @RolId, @Activo, NOW(), NOW());";

            using var connection = Connection;
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                await connection.ExecuteAsync(sql, new
                {
                    usuario.Id, usuario.NombreCompleto, usuario.Email, usuario.PasswordHash,
                    usuario.Activo, usuario.RolId, usuario.InstitucionId, usuario.EsSuperAdmin,
                    usuario.SedeId, EsSuperAdminUser = _sesion.EsSuperAdmin
                }, transaction);

                if (!usuario.EsSuperAdmin)
                {
                    var existe = await connection.ExecuteScalarAsync<int>(sqlCheckMovil, usuario, transaction);
                    if (existe > 0)
                        await connection.ExecuteAsync(sqlUpdateMovil, usuario, transaction);
                    else
                        await connection.ExecuteAsync(sqlInsertMovil, usuario, transaction);
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<bool> EliminarAsync(int id)
        {
            string sql;
            object parametros;

            if (DebeFiltrarPorInstitucion())
            {
                sql = "DELETE FROM usuarios WHERE id = @Id AND institucion_id = @InstitucionId";
                parametros = new { Id = id, InstitucionId = _sesion.InstitucionId };
            }
            else
            {
                sql = "DELETE FROM usuarios WHERE id = @Id";
                parametros = new { Id = id };
            }

            using var connection = Connection;
            var filas = await connection.ExecuteAsync(sql, parametros);
            return filas > 0;
        }

        public async Task<bool> ExisteEmailAsync(string email, int? excluirId = null)
        {
            const string sql = @"SELECT COUNT(*) FROM usuarios WHERE email = @Email AND (@ExcluirId IS NULL OR id <> @ExcluirId);";
            using var connection = Connection;
            var n = await connection.ExecuteScalarAsync<int>(sql, new { Email = email, ExcluirId = excluirId });
            return n > 0;
        }
    }
}