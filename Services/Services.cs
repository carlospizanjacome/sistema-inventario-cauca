using Dapper;
using Npgsql;
using System.Data;
using Almacen.Models;
namespace Almacen.Services
{
    public interface IAuthService
    {
        Task<bool> ValidarCredencialesAsync(string email, string password);
    }

    public class AuthService : IAuthService
    {
        private readonly IConfiguration _configuration;

        public AuthService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<bool> ValidarCredencialesAsync(string email, string password)
        {
            // Eliminamos el try-catch interno para permitir que los errores de conexión
            // se muestren claramente en la interfaz gráfica del Login
            string connectionString =
                _configuration.GetConnectionString("DefaultConnection")
                ?? throw new Exception("Error interno: No se encontró la cadena de conexión 'DefaultConnection' en la configuración.");

            using var connection = new NpgsqlConnection(connectionString);

            const string sql = @"
                SELECT password_hash
                FROM usuarios
                WHERE LOWER(email) = LOWER(@Email)
                AND activo = true
                LIMIT 1;
            ";

            var parametros = new DynamicParameters();
            parametros.Add("Email", email.Trim(), DbType.String);

            var passwordGuardado = await connection.QueryFirstOrDefaultAsync<string>(sql, parametros);

            // Si el correo no existe en la base de datos Neon o el usuario está inactivo
            if (string.IsNullOrWhiteSpace(passwordGuardado))
                return false;

            // VALIDACIÓN 1: Soporte temporal para contraseñas en texto plano (como tu ID 12: jose@gmail.com)
            if (passwordGuardado == password)
                return true;

            try
            {
                // VALIDACIÓN 2: Intenta verificar mediante el Hash seguro de BCrypt (como tus IDs 13, 14, 15)
                return BCrypt.Net.BCrypt.Verify(password, passwordGuardado);
            }
            catch (BCrypt.Net.BcryptAuthenticationException)
            {
                // Si el formato del hash en la base de datos está corrupto o mal formado, evitar colapso y denegar acceso
                return false;
            }
        }
    }
}
