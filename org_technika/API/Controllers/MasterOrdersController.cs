using API.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Security.Claims;
using System.Web.Http;

namespace API.Controllers
{
    [RoutePrefix("api/master/orders")]
    [Authorize]
    public class MasterOrdersController : ApiController
    {
        private readonly string _connectionString;

        public MasterOrdersController()
        {
            ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["ServiceCenterDb"];
            if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString))
                throw new ConfigurationErrorsException("Не найдена строка подключения ServiceCenterDb.");
            _connectionString = settings.ConnectionString;
        }

        [HttpGet, Route("")]
        public IHttpActionResult GetOrders()
        {
            int masterId;
            if (!TryGetMasterId(out masterId)) return StatusCode(HttpStatusCode.Forbidden);

            const string sql = @"
                SELECT o.OrderId, o.OrderNumber, s.StatusName, o.CreatedAt,
                       o.FaultDescription, o.Diagnosis, e.SerialNumber,
                       et.TypeName, mf.ManufacturerName, em.ModelName, c.FullName, c.Phone
                FROM dbo.KP_RepairOrders AS o
                INNER JOIN dbo.KP_Clients AS c ON c.ClientId = o.ClientId
                INNER JOIN dbo.KP_OrderStatuses AS s ON s.StatusId = o.StatusId
                INNER JOIN dbo.KP_Equipment AS e ON e.EquipmentId = o.EquipmentId
                INNER JOIN dbo.KP_EquipmentModels AS em ON em.EquipmentModelId = e.EquipmentModelId
                INNER JOIN dbo.KP_EquipmentTypes AS et ON et.EquipmentTypeId = em.EquipmentTypeId
                INNER JOIN dbo.KP_Manufacturers AS mf ON mf.ManufacturerId = em.ManufacturerId
                WHERE o.MasterId = @MasterId AND o.IsArchived = 0
                ORDER BY o.CreatedAt DESC, o.OrderId DESC;";

            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.Add("@MasterId", SqlDbType.Int).Value = masterId;
                    cn.Open();
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        List<object> orders = new List<object>();
                        while (r.Read())
                        {
                            orders.Add(new
                            {
                                OrderId = r.GetInt32(0),
                                OrderNumber = r.GetString(1),
                                StatusName = r.GetString(2),
                                CreatedAt = r.GetDateTime(3),
                                FaultDescription = r.GetString(4),
                                Diagnosis = r.IsDBNull(5) ? null : r.GetString(5),
                                SerialNumber = r.GetString(6),
                                EquipmentType = r.GetString(7),
                                Manufacturer = r.GetString(8),
                                ModelName = r.GetString(9),
                                ClientName = r.GetString(10),
                                ClientPhone = r.GetString(11)
                            });
                        }
                        return Ok(orders);
                    }
                }
            }
            catch (SqlException) { return InternalServerError(); }
        }

        [HttpGet, Route("{id:int}")]
        public IHttpActionResult GetOrder(int id)
        {
            int masterId;
            if (!TryGetMasterId(out masterId)) return StatusCode(HttpStatusCode.Forbidden);
            if (id <= 0) return BadRequest("Идентификатор заказа должен быть положительным числом.");

            const string sql = @"
                SELECT o.OrderId, o.OrderNumber, s.StatusName, o.CreatedAt,
                       o.FaultDescription, o.Diagnosis, o.CompletedAt, o.IssuedAt,
                        o.IsPaid, e.SerialNumber, et.TypeName, mf.ManufacturerName, em.ModelName, c.FullName, c.Phone
                FROM dbo.KP_RepairOrders AS o
                INNER JOIN dbo.KP_Clients AS c ON c.ClientId = o.ClientId
                INNER JOIN dbo.KP_OrderStatuses AS s ON s.StatusId = o.StatusId
                INNER JOIN dbo.KP_Equipment AS e ON e.EquipmentId = o.EquipmentId
                INNER JOIN dbo.KP_EquipmentModels AS em ON em.EquipmentModelId = e.EquipmentModelId
                INNER JOIN dbo.KP_EquipmentTypes AS et ON et.EquipmentTypeId = em.EquipmentTypeId
                INNER JOIN dbo.KP_Manufacturers AS mf ON mf.ManufacturerId = em.ManufacturerId
                WHERE o.OrderId = @OrderId AND o.MasterId = @MasterId AND o.IsArchived = 0;";

            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.Add("@OrderId", SqlDbType.Int).Value = id;
                    cmd.Parameters.Add("@MasterId", SqlDbType.Int).Value = masterId;
                    cn.Open();
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        if (!r.Read()) return NotFound();
                        return Ok(new
                        {
                            OrderId = r.GetInt32(0), OrderNumber = r.GetString(1),
                            StatusName = r.GetString(2), CreatedAt = r.GetDateTime(3),
                            FaultDescription = r.GetString(4), Diagnosis = r.IsDBNull(5) ? null : r.GetString(5),
                            CompletedAt = r.IsDBNull(6) ? (DateTime?)null : r.GetDateTime(6),
                            IssuedAt = r.IsDBNull(7) ? (DateTime?)null : r.GetDateTime(7),
                            IsPaid = r.GetBoolean(8), SerialNumber = r.GetString(9),
                            EquipmentType = r.GetString(10), Manufacturer = r.GetString(11), ModelName = r.GetString(12), ClientName = r.GetString(13), ClientPhone = r.GetString(14)
                        });
                    }
                }
            }
            catch (SqlException) { return InternalServerError(); }
        }

        [HttpPut, Route("{id:int}/diagnosis")]
        public IHttpActionResult UpdateDiagnosis(int id, UpdateDiagnosisRequest request)
        {
            int masterId;
            if (!TryGetMasterId(out masterId)) return StatusCode(HttpStatusCode.Forbidden);
            if (id <= 0 || request == null || !ModelState.IsValid) return BadRequest(ModelState);

            const string sql = @"
                UPDATE dbo.KP_RepairOrders
                SET Diagnosis = @Diagnosis
                WHERE OrderId = @OrderId AND MasterId = @MasterId AND IsArchived = 0;
                SELECT @@ROWCOUNT;";
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.Add("@OrderId", SqlDbType.Int).Value = id;
                    cmd.Parameters.Add("@MasterId", SqlDbType.Int).Value = masterId;
                    cmd.Parameters.Add("@Diagnosis", SqlDbType.NVarChar, -1).Value = request.Diagnosis.Trim();
                    cn.Open();
                    if ((int)cmd.ExecuteScalar() == 0) return NotFound();
                    return Ok(new { OrderId = id, Diagnosis = request.Diagnosis.Trim() });
                }
            }
            catch (SqlException) { return InternalServerError(); }
        }

        private bool TryGetMasterId(out int masterId)
        {
            masterId = 0;
            ClaimsIdentity identity = User.Identity as ClaimsIdentity;
            string role = identity == null ? null :
                (identity.FindFirst("role") ?? identity.FindFirst(ClaimTypes.Role))?.Value;
            string value = identity == null ? null : identity.FindFirst("masterId")?.Value;
            return role == "Мастер" && int.TryParse(value, out masterId) && masterId > 0;
        }
    }
}
