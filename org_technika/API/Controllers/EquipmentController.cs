using API.Security;
using API.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.Http;

namespace API.Controllers
{
    [RoutePrefix("api/clients/{clientId:int}/equipment")]
    [AdminOnly]
    public class EquipmentController : ApiController
    {
        private readonly string _connectionString;

        public EquipmentController()
        {
            ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["ServiceCenterDb"];
            if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString))
                throw new ConfigurationErrorsException("Не найдена строка подключения ServiceCenterDb.");
            _connectionString = settings.ConnectionString;
        }

        [HttpGet, Route("~/api/equipment/models")]
        public IHttpActionResult GetModels()
        {
            const string sql = @"
                SELECT em.EquipmentModelId, et.TypeName, mf.ManufacturerName, em.ModelName
                FROM dbo.KP_EquipmentModels AS em
                INNER JOIN dbo.KP_EquipmentTypes AS et ON et.EquipmentTypeId = em.EquipmentTypeId
                INNER JOIN dbo.KP_Manufacturers AS mf ON mf.ManufacturerId = em.ManufacturerId
                ORDER BY et.TypeName, mf.ManufacturerName, em.ModelName;";

            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(sql, cn))
                {
                    cn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        List<object> models = new List<object>();
                        while (reader.Read())
                        {
                            models.Add(new
                            {
                                EquipmentModelId = reader.GetInt32(0),
                                EquipmentType = reader.GetString(1),
                                Manufacturer = reader.GetString(2),
                                ModelName = reader.GetString(3)
                            });
                        }
                        return Ok(models);
                    }
                }
            }
            catch (SqlException)
            {
                return InternalServerError();
            }
        }

        [HttpGet, Route("")]
        public IHttpActionResult GetByClient(int clientId)
        {
            if (clientId <= 0)
                return BadRequest("Идентификатор клиента должен быть положительным числом.");

            const string sql = @"
                SELECT DISTINCT
                    e.EquipmentId,
                    e.SerialNumber,
                    et.TypeName AS EquipmentType,
                    mf.ManufacturerName AS Manufacturer,
                    em.ModelName
                FROM dbo.KP_Equipment AS e
                INNER JOIN dbo.KP_EquipmentModels AS em ON em.EquipmentModelId = e.EquipmentModelId
                INNER JOIN dbo.KP_EquipmentTypes AS et ON et.EquipmentTypeId = em.EquipmentTypeId
                INNER JOIN dbo.KP_Manufacturers AS mf ON mf.ManufacturerId = em.ManufacturerId
                INNER JOIN dbo.KP_ClientEquipment AS ce ON ce.EquipmentId = e.EquipmentId
                WHERE ce.ClientId = @ClientId AND e.IsActive = 1
                ORDER BY e.SerialNumber;";

            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.Add("@ClientId", SqlDbType.Int).Value = clientId;
                    cn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        List<object> equipment = new List<object>();
                        while (reader.Read())
                        {
                            equipment.Add(new
                            {
                                EquipmentId = reader.GetInt32(0),
                                SerialNumber = reader.GetString(1),
                                EquipmentType = reader.GetString(2),
                                Manufacturer = reader.GetString(3),
                                ModelName = reader.GetString(4)
                            });
                        }
                        return Ok(equipment);
                    }
                }
            }
            catch (SqlException)
            {
                return InternalServerError();
            }
        }

        [HttpPost, Route("")]
        public IHttpActionResult RegisterForClient(int clientId, RegisterEquipmentRequest request)
        {
            if (clientId <= 0 || request == null || !ModelState.IsValid)
                return BadRequest(ModelState);

            const string sql = @"
                IF NOT EXISTS (SELECT 1 FROM dbo.KP_Clients WHERE ClientId = @ClientId AND IsActive = 1)
                    SELECT N'CLIENT_NOT_FOUND' AS Result;
                ELSE IF NOT EXISTS (SELECT 1 FROM dbo.KP_Equipment WHERE EquipmentId = @EquipmentId AND IsActive = 1)
                    SELECT N'EQUIPMENT_NOT_FOUND' AS Result;
                ELSE IF EXISTS (SELECT 1 FROM dbo.KP_ClientEquipment WHERE ClientId = @ClientId AND EquipmentId = @EquipmentId)
                    SELECT N'ALREADY_REGISTERED' AS Result;
                ELSE
                BEGIN
                    INSERT dbo.KP_ClientEquipment (ClientId, EquipmentId)
                    VALUES (@ClientId, @EquipmentId);
                    SELECT N'CREATED' AS Result;
                END;";

            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.Add("@ClientId", SqlDbType.Int).Value = clientId;
                    cmd.Parameters.Add("@EquipmentId", SqlDbType.Int).Value = request.EquipmentId;
                    cn.Open();
                    string result = Convert.ToString(cmd.ExecuteScalar());
                    if (result == "CLIENT_NOT_FOUND") return NotFound();
                    if (result == "EQUIPMENT_NOT_FOUND") return Content(System.Net.HttpStatusCode.NotFound, new { message = "Техника не найдена." });
                    if (result == "ALREADY_REGISTERED") return Content(System.Net.HttpStatusCode.Conflict, new { message = "Эта техника уже зарегистрирована на клиента." });
                    if (result != "CREATED") return InternalServerError();
                    return Content(System.Net.HttpStatusCode.Created, new { ClientId = clientId, EquipmentId = request.EquipmentId });
                }
            }
            catch (SqlException)
            {
                return InternalServerError();
            }
        }

        [HttpPost, Route("~/api/equipment")]
        public IHttpActionResult Create(CreateEquipmentRequest request)
        {
            if (request == null || !ModelState.IsValid)
                return BadRequest(ModelState);

            const string sql = @"
                IF NOT EXISTS (SELECT 1 FROM dbo.KP_Clients WHERE ClientId = @ClientId AND IsActive = 1)
                    SELECT N'CLIENT_NOT_FOUND' AS Result;
                ELSE IF NOT EXISTS (SELECT 1 FROM dbo.KP_EquipmentModels WHERE EquipmentModelId = @ModelId)
                    SELECT N'MODEL_NOT_FOUND' AS Result;
                ELSE
                BEGIN
                    BEGIN TRANSACTION;
                    DECLARE @EquipmentId INT;
                    INSERT dbo.KP_Equipment (EquipmentModelId, SerialNumber, IsActive)
                    VALUES (@ModelId, @SerialNumber, 1);
                    SET @EquipmentId = CONVERT(INT, SCOPE_IDENTITY());
                    INSERT dbo.KP_ClientEquipment (ClientId, EquipmentId)
                    VALUES (@ClientId, @EquipmentId);
                    COMMIT TRANSACTION;
                    SELECT @EquipmentId;
                END;";

            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.Add("@ClientId", SqlDbType.Int).Value = request.ClientId;
                    cmd.Parameters.Add("@ModelId", SqlDbType.Int).Value = request.EquipmentModelId;
                    cmd.Parameters.Add("@SerialNumber", SqlDbType.NVarChar, 100).Value = request.SerialNumber.Trim();
                    cn.Open();
                    object result = cmd.ExecuteScalar();
                    if (result is string && (string)result == "CLIENT_NOT_FOUND") return NotFound();
                    if (result is string && (string)result == "MODEL_NOT_FOUND")
                        return Content(System.Net.HttpStatusCode.BadRequest, new { message = "Модель техники не найдена." });
                    int equipmentId = Convert.ToInt32(result);
                    return Content(System.Net.HttpStatusCode.Created, new
                    {
                        EquipmentId = equipmentId,
                        ClientId = request.ClientId,
                        EquipmentModelId = request.EquipmentModelId,
                        SerialNumber = request.SerialNumber.Trim()
                    });
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627 || ex.Number == 2601)
                    return Content(System.Net.HttpStatusCode.Conflict, new { message = "Серийный номер уже используется." });
                return InternalServerError();
            }
        }

        [HttpPut, Route("~/api/equipment/{id:int}")]
        public IHttpActionResult Update(int id, UpdateEquipmentRequest request)
        {
            if (id <= 0 || request == null || !ModelState.IsValid) return BadRequest(ModelState);
            const string sql = @"UPDATE dbo.KP_Equipment SET EquipmentModelId=@ModelId, SerialNumber=@SerialNumber
                WHERE EquipmentId=@Id AND IsActive=1; SELECT @@ROWCOUNT;";
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                    cmd.Parameters.Add("@ModelId", SqlDbType.Int).Value = request.EquipmentModelId;
                    cmd.Parameters.Add("@SerialNumber", SqlDbType.NVarChar, 100).Value = request.SerialNumber.Trim();
                    cn.Open();
                    if ((int)cmd.ExecuteScalar() == 0) return NotFound();
                    return Ok(new { EquipmentId = id, request.EquipmentModelId, SerialNumber = request.SerialNumber.Trim() });
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627 || ex.Number == 2601)
                    return Content(System.Net.HttpStatusCode.Conflict, new { message = "Серийный номер уже используется." });
                if (ex.Number == 547) return BadRequest("Указанная модель техники не найдена.");
                return InternalServerError();
            }
        }

        [HttpDelete, Route("~/api/equipment/{id:int}")]
        public IHttpActionResult Deactivate(int id)
        {
            if (id <= 0) return BadRequest("Идентификатор техники должен быть положительным числом.");
            const string sql = @"UPDATE dbo.KP_Equipment SET IsActive=0 WHERE EquipmentId=@Id AND IsActive=1; SELECT @@ROWCOUNT;";
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                    cn.Open();
                    return (int)cmd.ExecuteScalar() == 0
                        ? (IHttpActionResult)NotFound()
                        : StatusCode(System.Net.HttpStatusCode.NoContent);
                }
            }
            catch (SqlException) { return InternalServerError(); }
        }
    }
}
