using API.Models;
using API.Security;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Web.Http;

namespace API.Controllers
{
    [RoutePrefix("api/clients")]
    [AdminOnly]
    public class ClientsController : ApiController
    {
        private readonly string _connectionString;

        public ClientsController()
        {
            ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["ServiceCenterDb"];
            if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString))
                throw new ConfigurationErrorsException("Не найдена строка подключения ServiceCenterDb.");
            _connectionString = settings.ConnectionString;
        }

        [HttpGet, Route("")]
        public IHttpActionResult Get(string search = null)
        {
            const string sql = @"SELECT ClientId, FullName, Phone, Email, Address, CreatedAt
                FROM dbo.KP_Clients
                WHERE IsActive = 1 AND (@Search IS NULL OR FullName LIKE @Pattern OR Phone LIKE @Pattern)
                ORDER BY FullName;";
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(sql, cn))
                {
                    string value = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
                    cmd.Parameters.Add("@Search", SqlDbType.NVarChar, 300).Value = (object)value ?? DBNull.Value;
                    cmd.Parameters.Add("@Pattern", SqlDbType.NVarChar, 310).Value = (object)(value == null ? null : "%" + value + "%") ?? DBNull.Value;
                    cn.Open();
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        List<object> rows = new List<object>();
                        while (r.Read()) rows.Add(new {
                            ClientId = r.GetInt32(0), FullName = r.GetString(1), Phone = r.GetString(2),
                            Email = r.IsDBNull(3) ? null : r.GetString(3),
                            Address = r.IsDBNull(4) ? null : r.GetString(4), CreatedAt = r.GetDateTime(5)
                        });
                        return Ok(rows);
                    }
                }
            }
            catch (SqlException) { return InternalServerError(); }
        }

        [HttpGet, Route("{id:int}")]
        public IHttpActionResult GetById(int id)
        {
            const string sql = @"SELECT ClientId, FullName, Phone, Email, Address, CreatedAt
                FROM dbo.KP_Clients WHERE ClientId = @Id AND IsActive = 1;";
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                    cn.Open();
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        if (!r.Read()) return NotFound();
                        return Ok(new { ClientId = r.GetInt32(0), FullName = r.GetString(1), Phone = r.GetString(2),
                            Email = r.IsDBNull(3) ? null : r.GetString(3), Address = r.IsDBNull(4) ? null : r.GetString(4), CreatedAt = r.GetDateTime(5) });
                    }
                }
            }
            catch (SqlException) { return InternalServerError(); }
        }

        [HttpPost, Route("")]
        public IHttpActionResult Create(ClientWriteRequest request)
        {
            if (request == null || !ModelState.IsValid) return BadRequest(ModelState);
            const string sql = @"INSERT dbo.KP_Clients (FullName, Phone, Email, Address)
                OUTPUT INSERTED.ClientId VALUES (@Name, @Phone, @Email, @Address);";
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(sql, cn))
                {
                    AddClientParameters(cmd, request);
                    cn.Open();
                    int id = (int)cmd.ExecuteScalar();
                    return Content(HttpStatusCode.Created, new { ClientId = id });
                }
            }
            catch (SqlException) { return Content(HttpStatusCode.Conflict, new { message = "Не удалось создать клиента. Проверьте данные и ограничения БД." }); }
        }

        [HttpPut, Route("{id:int}")]
        public IHttpActionResult Update(int id, ClientWriteRequest request)
        {
            if (id <= 0 || request == null || !ModelState.IsValid) return BadRequest(ModelState);
            const string sql = @"UPDATE dbo.KP_Clients SET FullName=@Name, Phone=@Phone, Email=@Email, Address=@Address
                WHERE ClientId=@Id AND IsActive=1; SELECT @@ROWCOUNT;";
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(sql, cn))
                {
                    AddClientParameters(cmd, request);
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                    cn.Open();
                    return (int)cmd.ExecuteScalar() == 0 ? (IHttpActionResult)NotFound() : Ok(new { ClientId = id });
                }
            }
            catch (SqlException) { return Content(HttpStatusCode.Conflict, new { message = "Не удалось обновить клиента." }); }
        }

        [HttpDelete, Route("{id:int}")]
        public IHttpActionResult Deactivate(int id)
        {
            if (id <= 0) return BadRequest("Идентификатор клиента должен быть положительным числом.");
            const string sql = @"UPDATE dbo.KP_Clients SET IsActive=0 WHERE ClientId=@Id AND IsActive=1; SELECT @@ROWCOUNT;";
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                    cn.Open();
                    return (int)cmd.ExecuteScalar() == 0
                        ? (IHttpActionResult)NotFound()
                        : StatusCode(HttpStatusCode.NoContent);
                }
            }
            catch (SqlException) { return InternalServerError(); }
        }

        private static void AddClientParameters(SqlCommand cmd, ClientWriteRequest request)
        {
            cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = request.FullName.Trim();
            cmd.Parameters.Add("@Phone", SqlDbType.NVarChar, 30).Value = request.Phone.Trim();
            cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 254).Value = (object)request.Email ?? DBNull.Value;
            cmd.Parameters.Add("@Address", SqlDbType.NVarChar, 300).Value = (object)request.Address ?? DBNull.Value;
        }
    }
}
