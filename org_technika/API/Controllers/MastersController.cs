using API.Security;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Data;
using System;
using System.Net;
using API.Models;
using System.Web.Http;

namespace API.Controllers
{
    [RoutePrefix("api/masters")]
    [AdminOnly]
    public class MastersController : ApiController
    {
        private readonly string _connectionString;

        public MastersController()
        {
            ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["ServiceCenterDb"];
            if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString))
                throw new ConfigurationErrorsException("Не найдена строка подключения ServiceCenterDb.");
            _connectionString = settings.ConnectionString;
        }

        [HttpGet, Route("")]
        public IHttpActionResult GetActive()
        {
            const string sql = @"
                SELECT MasterId, FullName, Phone, Specialization
                FROM dbo.KP_Masters
                WHERE IsActive = 1
                ORDER BY FullName;";

            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(sql, cn))
                {
                    cn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        List<object> masters = new List<object>();
                        while (reader.Read())
                        {
                            masters.Add(new
                            {
                                MasterId = reader.GetInt32(0),
                                FullName = reader.GetString(1),
                                Phone = reader.GetString(2),
                                Specialization = reader.IsDBNull(3) ? null : reader.GetString(3)
                            });
                        }
                        return Ok(masters);
                    }
                }
            }
            catch (SqlException)
            {
                return InternalServerError();
            }
        }

        [HttpPost, Route("")]
        public IHttpActionResult Create(MasterWriteRequest request)
        {
            if (request == null || !ModelState.IsValid) return BadRequest(ModelState);
            const string sql = @"INSERT dbo.KP_Masters (FullName,Phone,Specialization,IsActive) OUTPUT INSERTED.MasterId
                VALUES (@Name,@Phone,@Specialization,1);";
            try { using(SqlConnection cn=new SqlConnection(_connectionString)) using(SqlCommand cmd=new SqlCommand(sql,cn))
                {AddParameters(cmd,request);cn.Open();int id=(int)cmd.ExecuteScalar();return Content(HttpStatusCode.Created,new {MasterId=id});} }
            catch(SqlException){return Content(HttpStatusCode.Conflict,new {message="Не удалось создать мастера. Проверьте данные."});}
        }

        [HttpPut, Route("{id:int}")]
        public IHttpActionResult Update(int id, MasterWriteRequest request)
        {
            if(id<=0 || request==null || !ModelState.IsValid)return BadRequest(ModelState);
            const string sql=@"UPDATE dbo.KP_Masters SET FullName=@Name,Phone=@Phone,Specialization=@Specialization WHERE MasterId=@Id;SELECT @@ROWCOUNT;";
            try {using(SqlConnection cn=new SqlConnection(_connectionString))using(SqlCommand cmd=new SqlCommand(sql,cn))
                {AddParameters(cmd,request);cmd.Parameters.Add("@Id",SqlDbType.Int).Value=id;cn.Open();return (int)cmd.ExecuteScalar()==0?(IHttpActionResult)NotFound():Ok(new {MasterId=id});}}
            catch(SqlException){return InternalServerError();}
        }

        [HttpDelete, Route("{id:int}")]
        public IHttpActionResult Deactivate(int id)
        {
            if(id<=0)return BadRequest("Идентификатор мастера должен быть положительным числом.");
            const string sql=@"UPDATE dbo.KP_Masters SET IsActive=0 WHERE MasterId=@Id AND IsActive=1;SELECT @@ROWCOUNT;";
            try {using(SqlConnection cn=new SqlConnection(_connectionString))using(SqlCommand cmd=new SqlCommand(sql,cn))
                {cmd.Parameters.Add("@Id",SqlDbType.Int).Value=id;cn.Open();return (int)cmd.ExecuteScalar()==0?(IHttpActionResult)NotFound():StatusCode(HttpStatusCode.NoContent);}}
            catch(SqlException){return InternalServerError();}
        }

        private static void AddParameters(SqlCommand cmd, MasterWriteRequest request)
        {
            cmd.Parameters.Add("@Name",SqlDbType.NVarChar,200).Value=request.FullName.Trim();
            cmd.Parameters.Add("@Phone",SqlDbType.NVarChar,30).Value=request.Phone.Trim();
            cmd.Parameters.Add("@Specialization",SqlDbType.NVarChar,150).Value=(object)request.Specialization??DBNull.Value;
        }
    }
}
