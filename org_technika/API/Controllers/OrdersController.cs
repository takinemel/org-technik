using API.Models;
using API.Security;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Web.Http;
//великая связь для получения карточки заказа
using System.Data.Common;

namespace API.Controllers
{
    [RoutePrefix("api/orders")]
    [Authorize]
    public class OrdersController : ApiController
    {
        private readonly string _connectionString;

        public OrdersController()
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

        [HttpGet, Route("~/api/order-statuses")]
        [Authorize]
        public IHttpActionResult GetStatuses()
        {
            string role = GetCurrentRole();
            if (role != "Администратор" && role != "Приемщик" && role != "Мастер") return StatusCode(HttpStatusCode.Forbidden);
            const string sql = "SELECT StatusId, StatusName FROM dbo.KP_OrderStatuses ORDER BY StatusId;";
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(sql, cn))
                {
                    cn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        List<object> rows = new List<object>();
                        while (reader.Read()) rows.Add(new { StatusId = reader.GetInt32(0), StatusName = reader.GetString(1) });
                        return Ok(rows);
                    }
                }
            }
            catch (SqlException) { return InternalServerError(); }
        }

        [HttpGet, Route("archive")]
        [AdminOnly]
        public IHttpActionResult GetArchivedOrders()
        {
            return GetOrders(null, null, null, null, true);
        }

        // GET /api/orders
        // GET /api/orders?statusId=2&masterId=5&dateFrom=2026-10-01&dateTo=2026-10-31
        [HttpGet]
        [Route("")]
        public IHttpActionResult GetOrders(
            int? statusId = null,
            int? masterId = null,
            string dateFrom = null,
            string dateTo = null,
            bool includeArchived = false)
        {
            string role = GetCurrentRole();
            if (role != "Администратор" && role != "Приемщик" && role != "Мастер")
            {
                return StatusCode(HttpStatusCode.Forbidden);
            }

            int? assignedMasterId = null;
            if (role == "Приемщик")
            {
                string masterIdClaim = GetCurrentClaim("masterId");
                int parsedMasterId;
                if (!int.TryParse(masterIdClaim, out parsedMasterId) || parsedMasterId <= 0)
                {
                    return StatusCode(HttpStatusCode.Forbidden);
                }

                assignedMasterId = parsedMasterId;
                // Приемщик не может подменить область выборки параметром запроса.
                masterId = assignedMasterId;
                includeArchived = false;
            }
            if (role == "Мастер")
            {
                int parsedMasterId;
                if (!int.TryParse(GetCurrentClaim("masterId"), out parsedMasterId) || parsedMasterId <= 0) return StatusCode(HttpStatusCode.Forbidden);
                masterId = parsedMasterId;
                includeArchived = false;
            }

            DateTime? parsedDateFrom;
            DateTime? parsedDateTo;

            if (!TryParseDate(dateFrom, out parsedDateFrom))
            {
                return BadRequest("Параметр dateFrom должен иметь формат yyyy-MM-dd.");
            }

            if (!TryParseDate(dateTo, out parsedDateTo))
            {
                return BadRequest("Параметр dateTo должен иметь формат yyyy-MM-dd.");
            }

            if (parsedDateFrom.HasValue &&
                parsedDateTo.HasValue &&
                parsedDateFrom.Value.Date > parsedDateTo.Value.Date)
            {
                return BadRequest("Дата начала периода не может быть позже даты окончания.");
            }

            List<OrderListItem> orders = new List<OrderListItem>();

            string sql = @"
                SELECT
                    o.OrderId,
                    o.OrderNumber,
                    c.FullName AS ClientName,
                    c.Phone AS ClientPhone,
                    e.SerialNumber,
                    et.TypeName AS EquipmentType,
                    mf.ManufacturerName AS Manufacturer,
                    em.ModelName,
                    m.FullName AS MasterName,
                    s.StatusName,
                    o.CreatedAt,
                    o.IsPaid,
                    o.IsArchived
                FROM dbo.KP_RepairOrders AS o
                INNER JOIN dbo.KP_Clients AS c
                    ON c.ClientId = o.ClientId
                INNER JOIN dbo.KP_Equipment AS e
                    ON e.EquipmentId = o.EquipmentId
                INNER JOIN dbo.KP_EquipmentModels AS em
                    ON em.EquipmentModelId = e.EquipmentModelId
                INNER JOIN dbo.KP_EquipmentTypes AS et
                    ON et.EquipmentTypeId = em.EquipmentTypeId
                INNER JOIN dbo.KP_Manufacturers AS mf
                    ON mf.ManufacturerId = em.ManufacturerId
                INNER JOIN dbo.KP_OrderStatuses AS s
                    ON s.StatusId = o.StatusId
                LEFT JOIN dbo.KP_Masters AS m
                    ON m.MasterId = o.MasterId
                WHERE
                    (@StatusId IS NULL OR o.StatusId = @StatusId)
                    AND (@Role = N'Администратор' OR o.MasterId = @MasterId)
                    AND (@IncludeArchived = 1 OR o.IsArchived = 0)
                    AND (@DateFrom IS NULL OR o.CreatedAt >= @DateFrom)
                    AND (@DateToExclusive IS NULL OR o.CreatedAt < @DateToExclusive)
                ORDER BY o.CreatedAt DESC, o.OrderId DESC;";

            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    command.Parameters.Add("@Role", SqlDbType.NVarChar, 50).Value = role;
                    command.Parameters.Add("@StatusId", SqlDbType.Int).Value =
                        (object)statusId ?? DBNull.Value;

                    command.Parameters.Add("@MasterId", SqlDbType.Int).Value =
                        (object)masterId ?? DBNull.Value;

                    command.Parameters.Add("@IncludeArchived", SqlDbType.Bit).Value =
                        includeArchived;

                    command.Parameters.Add("@DateFrom", SqlDbType.DateTime2).Value =
                        parsedDateFrom.HasValue
                            ? (object)parsedDateFrom.Value.Date
                            : DBNull.Value;

                    // верхняя граница не включается, чтобы весь день dateTo попал в результат.
                    DateTime? dateToExclusive = parsedDateTo.HasValue
                        ? parsedDateTo.Value.Date.AddDays(1)
                        : (DateTime?)null;

                    command.Parameters.Add("@DateToExclusive", SqlDbType.DateTime2).Value =
                        dateToExclusive.HasValue
                            ? (object)dateToExclusive.Value
                            : DBNull.Value;

                    connection.Open();

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            orders.Add(new OrderListItem
                            {
                                OrderId = reader.GetInt32(reader.GetOrdinal("OrderId")),
                                OrderNumber = reader.GetString(reader.GetOrdinal("OrderNumber")),
                                ClientName = reader.GetString(reader.GetOrdinal("ClientName")),
                                ClientPhone = reader.GetString(reader.GetOrdinal("ClientPhone")),
                                SerialNumber = reader.GetString(reader.GetOrdinal("SerialNumber")),
                                EquipmentType = reader.GetString(reader.GetOrdinal("EquipmentType")),
                                Manufacturer = reader.GetString(reader.GetOrdinal("Manufacturer")),
                                ModelName = reader.GetString(reader.GetOrdinal("ModelName")),
                                MasterName = reader.IsDBNull(reader.GetOrdinal("MasterName"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("MasterName")),
                                StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                                IsPaid = reader.GetBoolean(reader.GetOrdinal("IsPaid")),
                                IsArchived = reader.GetBoolean(reader.GetOrdinal("IsArchived"))
                            });
                        }
                    }
                }

                return Ok(orders);
            }
            catch (SqlException)
            {
                // не возвращает клиенту детали соединения и текст SQL-ошибки
                return Content(
                    HttpStatusCode.InternalServerError,
                    new { message = "Не удалось получить список заказов из базы данных." });
            }
        }

        private static bool TryParseDate(string value, out DateTime? result)
        {
            result = null;

            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            DateTime parsed;

            bool success = DateTime.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsed);

            if (!success)
            {
                return false;
            }

            result = parsed.Date;
            return true;
        }

        // GET /api/orders/15
        [HttpGet]
        [Route("{id:int}")]
        public IHttpActionResult GetOrder(int id)
        {
            string role = GetCurrentRole();
            if (role != "Администратор" && role != "Приемщик" && role != "Мастер")
            {
                return StatusCode(HttpStatusCode.Forbidden);
            }

            int? assignedMasterId = null;
            if (role == "Приемщик")
            {
                string masterIdClaim = GetCurrentClaim("masterId");
                int parsedMasterId;
                if (!int.TryParse(masterIdClaim, out parsedMasterId) || parsedMasterId <= 0)
                {
                    return StatusCode(HttpStatusCode.Forbidden);
                }

                assignedMasterId = parsedMasterId;
            }

            if (id <= 0)
            {
                return BadRequest("Идентификатор заказа должен быть положительным числом.");
            }

            const string orderSql = @"
        SELECT
            o.OrderId,
            o.OrderNumber,
            o.CreatedAt,
            o.FaultDescription,
            o.Diagnosis,
            o.CompletedAt,
            o.IssuedAt,
            o.IsPaid,
            o.IsArchived,

            s.StatusName,

            c.ClientId,
            c.FullName AS ClientFullName,
            c.Phone AS ClientPhone,
            c.Email AS ClientEmail,
            c.Address AS ClientAddress,

            e.EquipmentId,
            e.SerialNumber,
            et.TypeName,
            mf.ManufacturerName,
            em.ModelName,

            m.MasterId,
            m.FullName AS MasterFullName,
            m.Phone AS MasterPhone,
            m.Specialization AS MasterSpecialization
        FROM dbo.KP_RepairOrders AS o
        INNER JOIN dbo.KP_OrderStatuses AS s
            ON s.StatusId = o.StatusId
        INNER JOIN dbo.KP_Clients AS c
            ON c.ClientId = o.ClientId
        INNER JOIN dbo.KP_Equipment AS e
            ON e.EquipmentId = o.EquipmentId
        INNER JOIN dbo.KP_EquipmentModels AS em
            ON em.EquipmentModelId = e.EquipmentModelId
        INNER JOIN dbo.KP_EquipmentTypes AS et
            ON et.EquipmentTypeId = em.EquipmentTypeId
        INNER JOIN dbo.KP_Manufacturers AS mf
            ON mf.ManufacturerId = em.ManufacturerId
        LEFT JOIN dbo.KP_Masters AS m
            ON m.MasterId = o.MasterId
        WHERE o.OrderId = @OrderId
          AND (@Role = N'Администратор' OR o.MasterId = @MasterId)
          AND (@Role = N'Администратор' OR o.IsArchived = 0);

        SELECT
            RepairWorkId,
            WorkName,
            WorkDate,
            Price
        FROM dbo.KP_RepairWorks
        WHERE OrderId = @OrderId
          AND (@Role = N'Администратор' OR EXISTS
              (SELECT 1 FROM dbo.KP_RepairOrders AS ord WHERE ord.OrderId = @OrderId AND ord.MasterId = @MasterId AND ord.IsArchived = 0))
        ORDER BY WorkDate, RepairWorkId;

        SELECT
            op.SparePartId,
            sp.PartName,
            sp.Unit,
            op.Quantity,
            op.UnitPrice
        FROM dbo.KP_OrderParts AS op
        INNER JOIN dbo.KP_SpareParts AS sp
            ON sp.SparePartId = op.SparePartId
        WHERE op.OrderId = @OrderId
          AND (@Role = N'Администратор' OR EXISTS
              (SELECT 1 FROM dbo.KP_RepairOrders AS ord WHERE ord.OrderId = @OrderId AND ord.MasterId = @MasterId AND ord.IsArchived = 0))
        ORDER BY sp.PartName;

        SELECT
            s.StatusName,
            h.ChangedAt,
            ua.Login AS ChangedBy,
            h.Comment
        FROM dbo.KP_OrderStatusHistory AS h
        INNER JOIN dbo.KP_OrderStatuses AS s
            ON s.StatusId = h.StatusId
        LEFT JOIN dbo.KP_UserAccounts AS ua
            ON ua.UserId = h.ChangedByUserId
        WHERE h.OrderId = @OrderId
          AND (@Role = N'Администратор' OR EXISTS
              (SELECT 1 FROM dbo.KP_RepairOrders AS ord WHERE ord.OrderId = @OrderId AND ord.MasterId = @MasterId AND ord.IsArchived = 0))
        ORDER BY h.ChangedAt, h.OrderStatusHistoryId;";

            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                using (SqlCommand command = new SqlCommand(orderSql, connection))
                {
                    command.Parameters.Add("@OrderId", SqlDbType.Int).Value = id;
                    command.Parameters.Add("@Role", SqlDbType.NVarChar, 50).Value = role;
                    command.Parameters.Add("@MasterId", SqlDbType.Int).Value =
                        (object)assignedMasterId ?? DBNull.Value;

                    connection.Open();

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return NotFound();
                        }

                        OrderDetails order = new OrderDetails
                        {
                            OrderId = reader.GetInt32(reader.GetOrdinal("OrderId")),
                            OrderNumber = reader.GetString(reader.GetOrdinal("OrderNumber")),
                            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                            FaultDescription = reader.GetString(reader.GetOrdinal("FaultDescription")),
                            Diagnosis = ReadNullableString(reader, "Diagnosis"),
                            CompletedAt = ReadNullableDateTime(reader, "CompletedAt"),
                            IssuedAt = ReadNullableDateTime(reader, "IssuedAt"),
                            IsPaid = reader.GetBoolean(reader.GetOrdinal("IsPaid")),
                            IsArchived = reader.GetBoolean(reader.GetOrdinal("IsArchived")),
                            StatusName = reader.GetString(reader.GetOrdinal("StatusName")),

                            Client = new ClientDetails
                            {
                                ClientId = reader.GetInt32(reader.GetOrdinal("ClientId")),
                                FullName = reader.GetString(reader.GetOrdinal("ClientFullName")),
                                Phone = reader.GetString(reader.GetOrdinal("ClientPhone")),
                                Email = ReadNullableString(reader, "ClientEmail"),
                                Address = ReadNullableString(reader, "ClientAddress")
                            },

                            Equipment = new EquipmentDetails
                            {
                                EquipmentId = reader.GetInt32(reader.GetOrdinal("EquipmentId")),
                                SerialNumber = reader.GetString(reader.GetOrdinal("SerialNumber")),
                                TypeName = reader.GetString(reader.GetOrdinal("TypeName")),
                                ManufacturerName = reader.GetString(reader.GetOrdinal("ManufacturerName")),
                                ModelName = reader.GetString(reader.GetOrdinal("ModelName"))
                            },

                            Master = new MasterDetails
                            {
                                MasterId = ReadNullableInt(reader, "MasterId"),
                                FullName = ReadNullableString(reader, "MasterFullName"),
                                Phone = ReadNullableString(reader, "MasterPhone"),
                                Specialization = ReadNullableString(reader, "MasterSpecialization")
                            },

                            Works = new List<RepairWorkDetails>(),
                            Parts = new List<OrderPartDetails>(),
                            StatusHistory = new List<OrderStatusHistoryItem>()
                        };

                        // Перейти к набору строк выполненных работ.
                        if (reader.NextResult())
                        {
                            while (reader.Read())
                            {
                                order.Works.Add(new RepairWorkDetails
                                {
                                    RepairWorkId = reader.GetInt32(
                                        reader.GetOrdinal("RepairWorkId")),
                                    WorkName = reader.GetString(
                                        reader.GetOrdinal("WorkName")),
                                    WorkDate = reader.GetDateTime(
                                        reader.GetOrdinal("WorkDate")),
                                    Price = reader.GetDecimal(
                                        reader.GetOrdinal("Price"))
                                });
                            }
                        }

                        // Перейти к набору использованных запчастей.
                        if (reader.NextResult())
                        {
                            while (reader.Read())
                            {
                                decimal quantity = reader.GetDecimal(
                                    reader.GetOrdinal("Quantity"));

                                decimal unitPrice = reader.GetDecimal(
                                    reader.GetOrdinal("UnitPrice"));

                                order.Parts.Add(new OrderPartDetails
                                {
                                    SparePartId = reader.GetInt32(
                                        reader.GetOrdinal("SparePartId")),
                                    PartName = reader.GetString(
                                        reader.GetOrdinal("PartName")),
                                    Unit = reader.GetString(
                                        reader.GetOrdinal("Unit")),
                                    Quantity = quantity,
                                    UnitPrice = unitPrice,
                                    LineTotal = quantity * unitPrice
                                });
                            }
                        }

                        // Перейти к истории изменений статуса.
                        if (reader.NextResult())
                        {
                            while (reader.Read())
                            {
                                order.StatusHistory.Add(new OrderStatusHistoryItem
                                {
                                    StatusName = reader.GetString(
                                        reader.GetOrdinal("StatusName")),
                                    ChangedAt = reader.GetDateTime(
                                        reader.GetOrdinal("ChangedAt")),
                                    ChangedBy = ReadNullableString(reader, "ChangedBy"),
                                    Comment = ReadNullableString(reader, "Comment")
                                });
                            }
                        }

                        decimal worksTotal = 0m;

                        foreach (RepairWorkDetails work in order.Works)
                        {
                            worksTotal += work.Price;
                        }

                        decimal partsTotal = 0m;

                        foreach (OrderPartDetails part in order.Parts)
                        {
                            partsTotal += part.LineTotal;
                        }

                        order.TotalCost = worksTotal + partsTotal;

                        return Ok(order);
                    }
                }
            }
            catch (SqlException)
            {
                return Content(
                    HttpStatusCode.InternalServerError,
                    new { message = "Не удалось получить карточку заказа из базы данных." });
            }
        }

        private static string ReadNullableString(SqlDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);

            return reader.IsDBNull(ordinal)
                ? null
                : reader.GetString(ordinal);
        }

        [HttpPost, Route("")]
        [AdminOnly]
        public IHttpActionResult CreateOrder(CreateOrderRequest request)
        {
            if (request == null || !ModelState.IsValid)
                return BadRequest(ModelState);

            int userId;
            ClaimsIdentity identity = User.Identity as ClaimsIdentity;

            string userIdText = identity == null
                ? null
                : (identity.FindFirst("sub") ??
                   identity.FindFirst(ClaimTypes.NameIdentifier))?.Value;
            if (!int.TryParse(userIdText, out userId))
                return Unauthorized();

            const string sql = @"
                DECLARE @StatusId INT;
                SELECT @StatusId = StatusId FROM dbo.KP_OrderStatuses
                WHERE StatusName = N'В ожидании диагностики';
                IF @StatusId IS NULL THROW 50010, 'Initial status is missing', 1;
                IF NOT EXISTS (SELECT 1 FROM dbo.KP_Clients WHERE ClientId=@ClientId AND IsActive=1)
                    THROW 50011, 'Client not found', 1;
                IF NOT EXISTS (SELECT 1 FROM dbo.KP_Equipment WHERE EquipmentId=@EquipmentId AND IsActive=1)
                    THROW 50012, 'Equipment not found', 1;
                IF NOT EXISTS (SELECT 1 FROM dbo.KP_ClientEquipment
                               WHERE ClientId=@ClientId AND EquipmentId=@EquipmentId)
                    THROW 50014, 'Equipment is not registered to client', 1;
                IF @MasterId IS NOT NULL AND NOT EXISTS
                    (SELECT 1 FROM dbo.KP_Masters WHERE MasterId=@MasterId AND IsActive=1)
                    THROW 50013, 'Master not found or inactive', 1;

                INSERT dbo.KP_RepairOrders
                    (OrderNumber, ClientId, EquipmentId, MasterId, StatusId, CreatedByUserId,
                     CreatedAt, FaultDescription, IsPaid, IsArchived)
                OUTPUT INSERTED.OrderId
                VALUES (@OrderNumber, @ClientId, @EquipmentId, @MasterId, @StatusId, @UserId,
                        SYSUTCDATETIME(), @FaultDescription, 0, 0);";

            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.Add("@OrderNumber", SqlDbType.NVarChar, 30).Value = request.OrderNumber.Trim();
                    cmd.Parameters.Add("@ClientId", SqlDbType.Int).Value = request.ClientId;
                    cmd.Parameters.Add("@EquipmentId", SqlDbType.Int).Value = request.EquipmentId;
                    cmd.Parameters.Add("@MasterId", SqlDbType.Int).Value = (object)request.MasterId ?? DBNull.Value;
                    cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                    cmd.Parameters.Add("@FaultDescription", SqlDbType.NVarChar, -1).Value = request.FaultDescription.Trim();
                    cn.Open();
                    int orderId = (int)cmd.ExecuteScalar();
                    return Content(HttpStatusCode.Created, new { OrderId = orderId, request.OrderNumber });
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627 || ex.Number == 2601)
                    return Content(HttpStatusCode.Conflict, new { message = "Номер заказа уже используется." });
                if (ex.Number == 547)
                    return Content(HttpStatusCode.BadRequest, new { message = "Не найдены связанные клиент, техника или мастер." });
                return Content(HttpStatusCode.BadRequest, new { message = "Не удалось создать заказ. Проверьте клиента, технику, мастера и наличие начального статуса." });
            }
        }

        [HttpPut, Route("{id:int}/master")]
        [AdminOnly]
        public IHttpActionResult AssignMaster(int id, AssignMasterRequest request)
        {
            if (id <= 0 || request == null || !ModelState.IsValid) return BadRequest(ModelState);
            const string sql = @"
                UPDATE dbo.KP_RepairOrders
                SET MasterId=@MasterId
                WHERE OrderId=@OrderId AND IsArchived=0
                  AND EXISTS (SELECT 1 FROM dbo.KP_Masters WHERE MasterId=@MasterId AND IsActive=1);
                SELECT @@ROWCOUNT;";
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.Add("@MasterId", SqlDbType.Int).Value = request.MasterId;
                    cmd.Parameters.Add("@OrderId", SqlDbType.Int).Value = id;
                    cn.Open();
                    if ((int)cmd.ExecuteScalar() == 0) return NotFound();
                    return Ok(new { OrderId = id, MasterId = request.MasterId });
                }
            }
            catch (SqlException) { return InternalServerError(); }
        }

        [HttpDelete, Route("{id:int}")]
        [AdminOnly]
        public IHttpActionResult Archive(int id)
        {
            if (id <= 0) return BadRequest("Идентификатор заказа должен быть положительным числом.");
            const string sql = @"UPDATE dbo.KP_RepairOrders SET IsArchived=1
                WHERE OrderId=@OrderId AND IsArchived=0; SELECT @@ROWCOUNT;";
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.Add("@OrderId", SqlDbType.Int).Value = id;
                    cn.Open();
                    return (int)cmd.ExecuteScalar() == 0 ? (IHttpActionResult)NotFound() : StatusCode(HttpStatusCode.NoContent);
                }
            }
            catch (SqlException) { return InternalServerError(); }
        }

        [HttpPost, Route("{id:int}/works")]
        [AdminOnly]
        public IHttpActionResult AddWork(int id, OrderWorkRequest request)
        {
            if (id <= 0 || request == null || !ModelState.IsValid) return BadRequest(ModelState);
            const string sql = @"IF NOT EXISTS (SELECT 1 FROM dbo.KP_RepairOrders WHERE OrderId=@Id AND IsArchived=0) SELECT 0
                ELSE BEGIN INSERT dbo.KP_RepairWorks (OrderId, WorkName, Price) VALUES (@Id, @Name, @Price); SELECT CONVERT(INT,SCOPE_IDENTITY()); END;";
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                    cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = request.WorkName.Trim();
                    cmd.Parameters.Add("@Price", SqlDbType.Decimal).Value = request.Price;
                    cmd.Parameters["@Price"].Precision = 12; cmd.Parameters["@Price"].Scale = 2;
                    cn.Open(); int workId = (int)cmd.ExecuteScalar();
                    if (workId == 0) return NotFound();
                    return Content(HttpStatusCode.Created, new { RepairWorkId = workId, OrderId = id, WorkName = request.WorkName.Trim(), Price = request.Price });
                }
            }
            catch (SqlException) { return InternalServerError(); }
        }

        [HttpDelete, Route("{id:int}/works/{workId:int}")]
        [AdminOnly]
        public IHttpActionResult DeleteWork(int id, int workId)
        {
            if (id <= 0 || workId <= 0) return BadRequest("Идентификаторы должны быть положительными числами.");
            const string sql = @"DELETE dbo.KP_RepairWorks WHERE OrderId=@Id AND RepairWorkId=@WorkId AND EXISTS
                (SELECT 1 FROM dbo.KP_RepairOrders WHERE OrderId=@Id AND IsArchived=0); SELECT @@ROWCOUNT;";
            try { using (SqlConnection cn = new SqlConnection(_connectionString)) using (SqlCommand cmd = new SqlCommand(sql, cn))
                { cmd.Parameters.Add("@Id", SqlDbType.Int).Value=id; cmd.Parameters.Add("@WorkId", SqlDbType.Int).Value=workId; cn.Open(); return (int)cmd.ExecuteScalar()==0 ? (IHttpActionResult)NotFound() : StatusCode(HttpStatusCode.NoContent); } }
            catch (SqlException) { return InternalServerError(); }
        }

        [HttpPost, Route("{id:int}/parts")]
        [AdminOnly]
        public IHttpActionResult AddPart(int id, OrderPartRequest request)
        {
            if (id <= 0 || request == null || !ModelState.IsValid) return BadRequest(ModelState);
            const string sql = @"IF NOT EXISTS (SELECT 1 FROM dbo.KP_RepairOrders WHERE OrderId=@Id AND IsArchived=0) SELECT N'ORDER_NOT_FOUND'
                ELSE IF NOT EXISTS (SELECT 1 FROM dbo.KP_SpareParts WHERE SparePartId=@PartId AND IsActive=1) SELECT N'PART_NOT_FOUND'
                ELSE IF EXISTS (SELECT 1 FROM dbo.KP_OrderParts WHERE OrderId=@Id AND SparePartId=@PartId) SELECT N'ALREADY_ADDED'
                ELSE IF EXISTS (SELECT 1 FROM dbo.KP_SpareParts WHERE SparePartId=@PartId AND StockQuantity < @Quantity) SELECT N'INSUFFICIENT_STOCK'
                ELSE BEGIN INSERT dbo.KP_OrderParts (OrderId,SparePartId,Quantity,UnitPrice)
                    UPDATE dbo.KP_SpareParts SET StockQuantity=StockQuantity-@Quantity WHERE SparePartId=@PartId;
                    SELECT @Id,SparePartId,@Quantity,UnitPrice FROM dbo.KP_SpareParts WHERE SparePartId=@PartId;
                    SELECT N'CREATED' END;";
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString)) using (SqlCommand cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value=id; cmd.Parameters.Add("@PartId", SqlDbType.Int).Value=request.SparePartId;
                    SqlParameter quantity=cmd.Parameters.Add("@Quantity",SqlDbType.Decimal); quantity.Precision=12; quantity.Scale=3; quantity.Value=request.Quantity;
                    cn.Open(); string result=Convert.ToString(cmd.ExecuteScalar());
                    if(result=="ORDER_NOT_FOUND") return NotFound();
                    if(result=="PART_NOT_FOUND") return Content(HttpStatusCode.BadRequest,new {message="Активная запчасть не найдена."});
                    if(result=="ALREADY_ADDED") return Content(HttpStatusCode.Conflict,new {message="Запчасть уже добавлена в заказ."});
                    if(result=="INSUFFICIENT_STOCK") return Content(HttpStatusCode.Conflict,new {message="Недостаточно запчастей на складе."});
                    return Content(HttpStatusCode.Created,new {OrderId=id,SparePartId=request.SparePartId,Quantity=request.Quantity});
                }
            }
            catch(SqlException){return InternalServerError();}
        }

        [HttpDelete, Route("{id:int}/parts/{partId:int}")]
        [AdminOnly]
        public IHttpActionResult DeletePart(int id, int partId)
        {
            if(id<=0 || partId<=0) return BadRequest("Идентификаторы должны быть положительными числами.");
            const string sql=@"DELETE dbo.KP_OrderParts WHERE OrderId=@Id AND SparePartId=@PartId AND EXISTS
                (SELECT 1 FROM dbo.KP_RepairOrders WHERE OrderId=@Id AND IsArchived=0); SELECT @@ROWCOUNT;";
            try { using(SqlConnection cn=new SqlConnection(_connectionString)) using(SqlCommand cmd=new SqlCommand(sql,cn))
                {cmd.Parameters.Add("@Id",SqlDbType.Int).Value=id;cmd.Parameters.Add("@PartId",SqlDbType.Int).Value=partId;cn.Open();return (int)cmd.ExecuteScalar()==0?(IHttpActionResult)NotFound():StatusCode(HttpStatusCode.NoContent);} }
            catch(SqlException){return InternalServerError();}
        }

        [HttpGet, Route("~/api/clients/{clientId:int}/orders")]
        [AdminOnly]
        public IHttpActionResult GetClientOrders(int clientId)
        {
            if(clientId<=0) return BadRequest("Идентификатор клиента должен быть положительным числом.");
            const string sql=@"SELECT o.OrderId,o.OrderNumber,s.StatusName,o.CreatedAt,o.IsPaid,o.IsArchived,e.SerialNumber,
                et.TypeName,mf.ManufacturerName,em.ModelName,m.FullName AS MasterName
                FROM dbo.KP_RepairOrders o INNER JOIN dbo.KP_OrderStatuses s ON s.StatusId=o.StatusId
                INNER JOIN dbo.KP_Equipment e ON e.EquipmentId=o.EquipmentId INNER JOIN dbo.KP_EquipmentModels em ON em.EquipmentModelId=e.EquipmentModelId
                INNER JOIN dbo.KP_EquipmentTypes et ON et.EquipmentTypeId=em.EquipmentTypeId INNER JOIN dbo.KP_Manufacturers mf ON mf.ManufacturerId=em.ManufacturerId
                LEFT JOIN dbo.KP_Masters m ON m.MasterId=o.MasterId WHERE o.ClientId=@ClientId ORDER BY o.CreatedAt DESC,o.OrderId DESC;";
            try { using(SqlConnection cn=new SqlConnection(_connectionString)) using(SqlCommand cmd=new SqlCommand(sql,cn))
                {cmd.Parameters.Add("@ClientId",SqlDbType.Int).Value=clientId;cn.Open();using(SqlDataReader r=cmd.ExecuteReader())
                    {List<object> rows=new List<object>();while(r.Read()) rows.Add(new {OrderId=r.GetInt32(0),OrderNumber=r.GetString(1),StatusName=r.GetString(2),CreatedAt=r.GetDateTime(3),IsPaid=r.GetBoolean(4),IsArchived=r.GetBoolean(5),SerialNumber=r.GetString(6),EquipmentType=r.GetString(7),Manufacturer=r.GetString(8),ModelName=r.GetString(9),MasterName=r.IsDBNull(10)?null:r.GetString(10)});return Ok(rows);}}}
            catch(SqlException){return InternalServerError();}
        }

        [HttpPatch, Route("{id:int}/status")]
        [Authorize]
        public IHttpActionResult ChangeStatus(int id, ChangeOrderStatusRequest request)
        {
            if (id <= 0 || request == null || !ModelState.IsValid)
                return BadRequest(ModelState);

            string role = GetCurrentRole();
            if (role != "Администратор" && role != "Приемщик" && role != "Мастер")
                return StatusCode(HttpStatusCode.Forbidden);

            ClaimsIdentity identity = User.Identity as ClaimsIdentity;
            string userIdValue = identity == null ? null :
                (identity.FindFirst("sub") ?? identity.FindFirst(ClaimTypes.NameIdentifier))?.Value;
            int userId;
            if (!int.TryParse(userIdValue, out userId))
                return Unauthorized();

            int? masterId = null;
            if (role == "Приемщик")
            {
                int parsedMasterId;
                if (!int.TryParse(GetCurrentClaim("masterId"), out parsedMasterId) || parsedMasterId <= 0)
                    return StatusCode(HttpStatusCode.Forbidden);
                masterId = parsedMasterId;
            }
            if (role == "Мастер")
            {
                int parsedMasterId;
                if (!int.TryParse(GetCurrentClaim("masterId"), out parsedMasterId) || parsedMasterId <= 0) return StatusCode(HttpStatusCode.Forbidden);
                masterId = parsedMasterId;
            }

            const string sql = @"
                SET XACT_ABORT ON;
                BEGIN TRANSACTION;

                DECLARE @TargetStatusId INT;
                DECLARE @CurrentStatusName NVARCHAR(60);
                DECLARE @CompletedAt DATETIME2(0);
                DECLARE @IsPaid BIT;
                DECLARE @IsArchived BIT;
                DECLARE @CurrentMasterId INT;

                SELECT @TargetStatusId = StatusId
                FROM dbo.KP_OrderStatuses
                WHERE StatusName = @StatusName;

                IF @TargetStatusId IS NULL
                BEGIN
                    ROLLBACK TRANSACTION;
                    SELECT N'STATUS_NOT_FOUND' AS Result;
                    RETURN;
                END;

                SELECT
                    @CurrentStatusName = s.StatusName,
                    @CompletedAt = o.CompletedAt,
                    @IsPaid = o.IsPaid,
                    @IsArchived = o.IsArchived,
                    @CurrentMasterId = o.MasterId
                FROM dbo.KP_RepairOrders AS o WITH (UPDLOCK, ROWLOCK)
                INNER JOIN dbo.KP_OrderStatuses AS s ON s.StatusId = o.StatusId
                WHERE o.OrderId = @OrderId;

                IF @CurrentStatusName IS NULL
                BEGIN
                    ROLLBACK TRANSACTION;
                    SELECT N'ORDER_NOT_FOUND' AS Result;
                    RETURN;
                END;

                IF @IsArchived = 1
                BEGIN
                    ROLLBACK TRANSACTION;
                    SELECT N'ORDER_ARCHIVED' AS Result;
                    RETURN;
                END;

                IF @Role IN (N'Приемщик', N'Мастер') AND (@MasterId IS NULL OR @CurrentMasterId <> @MasterId)
                BEGIN
                    ROLLBACK TRANSACTION;
                    SELECT N'FORBIDDEN' AS Result;
                    RETURN;
                END;

                IF @CurrentStatusName = N'Выдан' OR @StatusName = N'Выдан'
                BEGIN
                    ROLLBACK TRANSACTION;
                    SELECT N'ISSUE_FORBIDDEN' AS Result;
                    RETURN;
                END;

                UPDATE dbo.KP_RepairOrders
                SET StatusId = @TargetStatusId,
                    CompletedAt = CASE
                        WHEN @StatusName = N'Готов' AND CompletedAt IS NULL THEN SYSUTCDATETIME()
                        ELSE CompletedAt
                    END,
                    IssuedAt = CASE
                        WHEN @StatusName = N'Выдан' AND IssuedAt IS NULL THEN SYSUTCDATETIME()
                        ELSE IssuedAt
                    END
                WHERE OrderId = @OrderId;

                INSERT dbo.KP_OrderStatusHistory
                    (OrderId, StatusId, ChangedByUserId, ChangedAt, Comment)
                VALUES
                    (@OrderId, @TargetStatusId, @UserId, SYSUTCDATETIME(), @Comment);

                COMMIT TRANSACTION;
                SELECT N'OK' AS Result;";

            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.Add("@OrderId", SqlDbType.Int).Value = id;
                    cmd.Parameters.Add("@StatusName", SqlDbType.NVarChar, 60).Value = request.StatusName.Trim();
                    cmd.Parameters.Add("@Role", SqlDbType.NVarChar, 50).Value = role;
                    cmd.Parameters.Add("@MasterId", SqlDbType.Int).Value = (object)masterId ?? DBNull.Value;
                    cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                    cmd.Parameters.Add("@Comment", SqlDbType.NVarChar, 500).Value = (object)request.Comment ?? DBNull.Value;
                    cn.Open();
                    string result = Convert.ToString(cmd.ExecuteScalar());

                    if (result == "STATUS_NOT_FOUND") return BadRequest("Указанный статус не найден.");
                    if (result == "ORDER_NOT_FOUND") return NotFound();
                    if (result == "ORDER_ARCHIVED") return Content(HttpStatusCode.Conflict, new { message = "Архивный заказ нельзя менять." });
                    if (result == "FORBIDDEN") return StatusCode(HttpStatusCode.Forbidden);
                    if (result == "ISSUE_FORBIDDEN") return Content(HttpStatusCode.Conflict, new { message = "Статус «Выдан» нельзя устанавливать или изменять." });
                    if (result == "COMPLETION_DATE_REQUIRED") return Content(HttpStatusCode.Conflict, new { message = "Нельзя установить статус Готов без завершённых ремонтных работ." });
                    if (result != "OK") return InternalServerError();

                    return Ok(new { OrderId = id, StatusName = request.StatusName.Trim() });
                }
            }
            catch (SqlException)
            {
                return Content(HttpStatusCode.InternalServerError, new { message = "Не удалось изменить статус заказа." });
            }
        }

        [HttpPost, Route("{id:int}/payment")]
        [AdminOnly]
        public IHttpActionResult MarkPaid(int id)
        {
            if (id <= 0)
                return BadRequest("Идентификатор заказа должен быть положительным числом.");

            const string sql = @"
                SET XACT_ABORT ON;
                BEGIN TRANSACTION;
                DECLARE @Archived BIT, @CompletedAt DATETIME2(0), @AlreadyPaid BIT;
                SELECT @Archived=IsArchived, @CompletedAt=CompletedAt, @AlreadyPaid=IsPaid
                FROM dbo.KP_RepairOrders WITH (UPDLOCK, ROWLOCK)
                WHERE OrderId=@OrderId;
                IF @Archived IS NULL
                BEGIN
                    ROLLBACK TRANSACTION;
                    SELECT N'NOT_FOUND' AS Result;
                    RETURN;
                END;
                IF @Archived=1
                BEGIN
                    ROLLBACK TRANSACTION;
                    SELECT N'ARCHIVED' AS Result;
                    RETURN;
                END;
                IF @CompletedAt IS NULL
                BEGIN
                    ROLLBACK TRANSACTION;
                    SELECT N'NOT_COMPLETED' AS Result;
                    RETURN;
                END;
                IF @AlreadyPaid=1
                BEGIN
                    COMMIT TRANSACTION;
                    SELECT N'ALREADY_PAID' AS Result;
                    RETURN;
                END;
                UPDATE dbo.KP_RepairOrders SET IsPaid=1 WHERE OrderId=@OrderId;
                COMMIT TRANSACTION;
                SELECT N'OK' AS Result;";

            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.Add("@OrderId", SqlDbType.Int).Value = id;
                    cn.Open();
                    string result = Convert.ToString(cmd.ExecuteScalar());
                    if (result == "OK")
                        return Ok(new { OrderId = id, IsPaid = true });
                    if (result == "ALREADY_PAID")
                        return Content(HttpStatusCode.Conflict, new { message = "Заказ уже отмечен как оплаченный." });
                    if (result == "NOT_FOUND") return NotFound();
                    if (result == "ARCHIVED") return Content(HttpStatusCode.Conflict, new { message = "Архивный заказ нельзя оплачивать." });
                    if (result == "NOT_COMPLETED") return Content(HttpStatusCode.Conflict, new { message = "Отметить оплату можно только после завершения ремонта." });
                    return InternalServerError();
                }
            }
            catch (SqlException)
            {
                return Content(HttpStatusCode.InternalServerError, new { message = "Не удалось отметить заказ оплаченным." });
            }
        }

        private string GetCurrentRole()
        {
            ClaimsPrincipal principal = User as ClaimsPrincipal;
            return principal == null ? null :
                (principal.FindFirst("role") ?? principal.FindFirst(ClaimTypes.Role))?.Value;
        }

        private string GetCurrentClaim(string type)
        {
            ClaimsPrincipal principal = User as ClaimsPrincipal;
            return principal == null ? null : principal.FindFirst(type)?.Value;
        }

        private static DateTime? ReadNullableDateTime(
            SqlDataReader reader,
            string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);

            return reader.IsDBNull(ordinal)
                ? (DateTime?)null
                : reader.GetDateTime(ordinal);
        }

        private static int? ReadNullableInt(SqlDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);

            return reader.IsDBNull(ordinal)
                ? (int?)null
                : reader.GetInt32(ordinal);
        }
    }
}
