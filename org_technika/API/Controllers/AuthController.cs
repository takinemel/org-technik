using API.Models;
using API.Security;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Web.Http;

namespace API.Controllers
{
    [RoutePrefix("api/auth")]
    public class AuthController : ApiController
    {
        private readonly string _connectionString;

        public AuthController()
        {
            ConnectionStringSettings settings =
                ConfigurationManager.ConnectionStrings["ServiceCenterDb"];

            if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString))
            {
                throw new ConfigurationErrorsException(
                    "В Web.config не найдена строка подключения ServiceCenterDb.");
            }

            _connectionString = settings.ConnectionString;
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("login")]
        public IHttpActionResult Login(LoginRequest request)
        {
            if (request == null || !ModelState.IsValid)
            {
                return BadRequest("Необходимо передать логин и пароль.");
            }

            int userId;
            string login;
            string passwordHash;
            string roleName;
            bool isActive;
            int? masterId;

            const string sql = @"
                SELECT TOP (1)
                    u.UserId,
                    u.Login,
                    u.PasswordHash,
                    u.IsActive,
                    u.MasterId,
                    r.RoleName
                FROM dbo.KP_UserAccounts AS u
                INNER JOIN dbo.KP_UserRoles AS r ON r.RoleId = u.RoleId
                WHERE u.Login = @Login;";

            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    command.Parameters.Add("@Login", SqlDbType.NVarChar, 100).Value =
                        request.Login.Trim();
                    connection.Open();

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return UnauthorizedResult();
                        }

                        userId = reader.GetInt32(reader.GetOrdinal("UserId"));
                        login = reader.GetString(reader.GetOrdinal("Login"));
                        passwordHash = reader.GetString(reader.GetOrdinal("PasswordHash"));
                        isActive = reader.GetBoolean(reader.GetOrdinal("IsActive"));
                        masterId = reader.IsDBNull(reader.GetOrdinal("MasterId"))
                            ? (int?)null
                            : reader.GetInt32(reader.GetOrdinal("MasterId"));
                        roleName = reader.GetString(reader.GetOrdinal("RoleName"));
                    }
                }
            }
            catch (SqlException)
            {
                return InternalServerError();
            }

            if (!isActive || !PasswordHasher.VerifyPassword(request.Password, passwordHash))
            {
                return UnauthorizedResult();
            }

            if (roleName == "Приемщик" && !masterId.HasValue)
            {
                return Content(
                    System.Net.HttpStatusCode.Forbidden,
                    new { message = "Для учётной записи Приемщика не привязан мастер." });
            }

            try
            {
                LoginResponse response = CreateToken(userId, login, roleName, masterId);
                return Ok(response);
            }
            catch (ConfigurationErrorsException)
            {
                return InternalServerError();
            }
        }

        private LoginResponse CreateToken(int userId, string login, string roleName, int? masterId)
        {
            string signingKeyText = ConfigurationManager.AppSettings["Jwt:SigningKey"];
            string issuer = ConfigurationManager.AppSettings["Jwt:Issuer"];
            string audience = ConfigurationManager.AppSettings["Jwt:Audience"];
            string lifetimeText = ConfigurationManager.AppSettings["Jwt:LifetimeMinutes"];

            int lifetimeMinutes;
            if (string.IsNullOrWhiteSpace(signingKeyText) ||
                signingKeyText.StartsWith("REPLACE_", StringComparison.Ordinal) ||
                Encoding.UTF8.GetByteCount(signingKeyText) < 32 ||
                string.IsNullOrWhiteSpace(issuer) ||
                string.IsNullOrWhiteSpace(audience) ||
                !int.TryParse(lifetimeText, out lifetimeMinutes) ||
                lifetimeMinutes < 1 || lifetimeMinutes > 1440)
            {
                throw new ConfigurationErrorsException("Параметры JWT в Web.config настроены неверно.");
            }

            DateTime now = DateTime.UtcNow;
            DateTime expires = now.AddMinutes(lifetimeMinutes);
            byte[] key = Encoding.UTF8.GetBytes(signingKeyText);

            Claim[] claims =
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, login),
                new Claim("role", roleName),
                new Claim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", roleName)
            };

            if (masterId.HasValue)
            {
                Array.Resize(ref claims, claims.Length + 1);
                claims[claims.Length - 1] = new Claim("masterId", masterId.Value.ToString());
            }

            JwtSecurityToken token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: now,
                expires: expires,
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256));

            return new LoginResponse
            {
                AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
                TokenType = "Bearer",
                ExpiresAtUtc = expires,
                UserId = userId,
                Login = login,
                Role = roleName
            };
        }

        private IHttpActionResult UnauthorizedResult()
        {
            return Content(
                System.Net.HttpStatusCode.Unauthorized,
                new { message = "Неверный логин или пароль, либо учётная запись отключена." });
        }
    }
}
