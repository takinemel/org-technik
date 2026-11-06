using API.Models;
using API.Security;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Web.Http;

namespace API.Controllers
{
    [RoutePrefix("api/spare-parts")]
    [AdminOnly]
    public class SparePartsController : ApiController
    {
        private readonly string _connectionString;
        public SparePartsController()
        {
            ConnectionStringSettings settings=ConfigurationManager.ConnectionStrings["ServiceCenterDb"];
            if(settings==null||string.IsNullOrWhiteSpace(settings.ConnectionString))throw new ConfigurationErrorsException("Не найдена строка подключения ServiceCenterDb.");
            _connectionString=settings.ConnectionString;
        }

        [HttpGet,Route("")]
        public IHttpActionResult GetActive()
        {
            const string sql="SELECT SparePartId,PartName,Unit,UnitPrice,StockQuantity FROM dbo.KP_SpareParts WHERE IsActive=1 ORDER BY PartName;";
            try{using(SqlConnection cn=new SqlConnection(_connectionString))using(SqlCommand cmd=new SqlCommand(sql,cn)){cn.Open();using(SqlDataReader r=cmd.ExecuteReader()){List<object> rows=new List<object>();while(r.Read())rows.Add(new{SparePartId=r.GetInt32(0),PartName=r.GetString(1),Unit=r.GetString(2),UnitPrice=r.GetDecimal(3)});return Ok(rows);}}}
            catch(SqlException){return InternalServerError();}
        }

        [HttpPost,Route("")]
        public IHttpActionResult Create(SparePartWriteRequest request)
        {
            if(request==null||!ModelState.IsValid)return BadRequest(ModelState);
            const string sql="INSERT dbo.KP_SpareParts(PartName,Unit,UnitPrice,StockQuantity) OUTPUT INSERTED.SparePartId VALUES(@Name,@Unit,@Price,@StockQuantity);";
            try{using(SqlConnection cn=new SqlConnection(_connectionString))using(SqlCommand cmd=new SqlCommand(sql,cn)){AddParameters(cmd,request);cn.Open();int id=(int)cmd.ExecuteScalar();return Content(HttpStatusCode.Created,new{SparePartId=id});}}
            catch(SqlException ex){if(ex.Number==2627||ex.Number==2601)return Content(HttpStatusCode.Conflict,new{message="Запчасть с таким названием уже существует."});return InternalServerError();}
        }

        [HttpPut,Route("{id:int}")]
        public IHttpActionResult Update(int id,SparePartWriteRequest request)
        {
            if(id<=0||request==null||!ModelState.IsValid)return BadRequest(ModelState);
            const string sql="UPDATE dbo.KP_SpareParts SET PartName=@Name,Unit=@Unit,UnitPrice=@Price,StockQuantity=@StockQuantity WHERE SparePartId=@Id AND IsActive=1;SELECT @@ROWCOUNT;";
            try{using(SqlConnection cn=new SqlConnection(_connectionString))using(SqlCommand cmd=new SqlCommand(sql,cn)){AddParameters(cmd,request);cmd.Parameters.Add("@Id",SqlDbType.Int).Value=id;cn.Open();return(int)cmd.ExecuteScalar()==0?(IHttpActionResult)NotFound():Ok(new{SparePartId=id});}}
            catch(SqlException){return Content(HttpStatusCode.Conflict,new{message="Не удалось обновить запчасть."});}
        }

        [HttpDelete,Route("{id:int}")]
        public IHttpActionResult Deactivate(int id)
        {
            if(id<=0)return BadRequest("Идентификатор должен быть положительным числом.");
            const string sql="UPDATE dbo.KP_SpareParts SET IsActive=0 WHERE SparePartId=@Id AND IsActive=1;SELECT @@ROWCOUNT;";
            try{using(SqlConnection cn=new SqlConnection(_connectionString))using(SqlCommand cmd=new SqlCommand(sql,cn)){cmd.Parameters.Add("@Id",SqlDbType.Int).Value=id;cn.Open();return(int)cmd.ExecuteScalar()==0?(IHttpActionResult)NotFound():StatusCode(HttpStatusCode.NoContent);}}
            catch(SqlException){return InternalServerError();}
        }

        private static void AddParameters(SqlCommand cmd,SparePartWriteRequest request)
        {
            cmd.Parameters.Add("@Name",SqlDbType.NVarChar,200).Value=request.PartName.Trim();
            cmd.Parameters.Add("@Unit",SqlDbType.NVarChar,30).Value=request.Unit.Trim();
            SqlParameter price=cmd.Parameters.Add("@Price",SqlDbType.Decimal);price.Precision=12;price.Scale=2;price.Value=request.UnitPrice;
            SqlParameter stock=cmd.Parameters.Add("@StockQuantity",SqlDbType.Decimal);stock.Precision=12;stock.Scale=3;stock.Value=request.StockQuantity;
        }
    }
}
