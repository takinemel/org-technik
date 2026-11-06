using API.Security;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Net;
using System.Web.Http;

namespace API.Controllers
{
    [RoutePrefix("api/reports")]
    [AdminOnly]
    public sealed class ReportsController : ApiController
    {
        private readonly string _connectionString;
        public ReportsController()
        {
            ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["ServiceCenterDb"];
            if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString)) throw new ConfigurationErrorsException("Не найдена строка подключения ServiceCenterDb.");
            _connectionString = settings.ConnectionString;
        }

        [HttpGet, Route("sales")]
        public IHttpActionResult Sales(string dateFrom = null, string dateTo = null)
        {
            DateTime from; DateTime to;
            if (!TryReadPeriod(dateFrom, dateTo, out from, out to)) return BadRequest("Период должен быть задан в формате yyyy-MM-dd, а дата начала не может быть позже даты окончания.");
            const string sql = @"SELECT COUNT(DISTINCT o.OrderId), SUM(CASE WHEN o.IsPaid=1 THEN COALESCE(w.WorksTotal,0)+COALESCE(p.PartsTotal,0) ELSE 0 END), SUM(COALESCE(w.WorksTotal,0)+COALESCE(p.PartsTotal,0)) FROM dbo.KP_RepairOrders o OUTER APPLY (SELECT SUM(Price) WorksTotal FROM dbo.KP_RepairWorks WHERE OrderId=o.OrderId) w OUTER APPLY (SELECT SUM(Quantity*UnitPrice) PartsTotal FROM dbo.KP_OrderParts WHERE OrderId=o.OrderId) p WHERE o.IsArchived=0 AND o.CreatedAt>=@FromDate AND o.CreatedAt<@ToDateExclusive;";
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString)) using (SqlCommand cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.Add("@FromDate", SqlDbType.DateTime2).Value = from; cmd.Parameters.Add("@ToDateExclusive", SqlDbType.DateTime2).Value = to.AddDays(1); cn.Open();
                    using (SqlDataReader r = cmd.ExecuteReader()) { r.Read(); return Ok(new { DateFrom = from.ToString("yyyy-MM-dd"), DateTo = to.ToString("yyyy-MM-dd"), OrdersCount = r.GetInt32(0), PaidTotal = r.IsDBNull(1) ? 0m : r.GetDecimal(1), AccruedTotal = r.IsDBNull(2) ? 0m : r.GetDecimal(2) }); }
                }
            }
            catch (SqlException) { return Content(HttpStatusCode.InternalServerError, new { message = "Не удалось сформировать отчёт по выручке." }); }
        }

        [HttpGet, Route("workload")]
        public IHttpActionResult Workload(string dateFrom = null, string dateTo = null)
        {
            DateTime from; DateTime to;
            if (!TryReadPeriod(dateFrom, dateTo, out from, out to)) return BadRequest("Период должен быть задан в формате yyyy-MM-dd, а дата начала не может быть позже даты окончания.");
            const string sql = @"SELECT m.MasterId,m.FullName,COUNT(o.OrderId),COALESCE(SUM(CASE WHEN s.StatusName IN (N'Готов',N'Выдан') THEN 1 ELSE 0 END),0),COALESCE(SUM(COALESCE(w.WorksTotal,0)+COALESCE(p.PartsTotal,0)),0) FROM dbo.KP_Masters m LEFT JOIN dbo.KP_RepairOrders o ON o.MasterId=m.MasterId AND o.IsArchived=0 AND o.CreatedAt>=@FromDate AND o.CreatedAt<@ToDateExclusive LEFT JOIN dbo.KP_OrderStatuses s ON s.StatusId=o.StatusId OUTER APPLY (SELECT SUM(Price) WorksTotal FROM dbo.KP_RepairWorks WHERE OrderId=o.OrderId) w OUTER APPLY (SELECT SUM(Quantity*UnitPrice) PartsTotal FROM dbo.KP_OrderParts WHERE OrderId=o.OrderId) p WHERE m.IsActive=1 GROUP BY m.MasterId,m.FullName ORDER BY m.FullName;";
            try
            {
                List<object> rows = new List<object>();
                using (SqlConnection cn = new SqlConnection(_connectionString)) using (SqlCommand cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.Add("@FromDate", SqlDbType.DateTime2).Value = from; cmd.Parameters.Add("@ToDateExclusive", SqlDbType.DateTime2).Value = to.AddDays(1); cn.Open();
                    using (SqlDataReader r = cmd.ExecuteReader()) while (r.Read()) rows.Add(new { MasterId = r.GetInt32(0), FullName = r.GetString(1), OrdersCount = r.GetInt32(2), CompletedCount = r.GetInt32(3), Amount = r.GetDecimal(4) });
                }
                return Ok(new { DateFrom = from.ToString("yyyy-MM-dd"), DateTo = to.ToString("yyyy-MM-dd"), Items = rows });
            }
            catch (SqlException) { return Content(HttpStatusCode.InternalServerError, new { message = "Не удалось сформировать отчёт по загрузке мастеров." }); }
        }

        private static bool TryReadPeriod(string dateFrom, string dateTo, out DateTime from, out DateTime to)
        {
            // Не используем DateTime.MaxValue: верхняя граница периода увеличивается на один день.
            from = new DateTime(2000, 1, 1);
            to = DateTime.UtcNow.Date;
            if (!string.IsNullOrWhiteSpace(dateFrom) && !DateTime.TryParseExact(dateFrom, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out from)) return false;
            if (!string.IsNullOrWhiteSpace(dateTo) && !DateTime.TryParseExact(dateTo, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out to)) return false;
            return from.Date <= to.Date;
        }
    }
}
