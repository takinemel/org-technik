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
    [RoutePrefix("api/equipment-catalog")]
    [AdminOnly]
    public class EquipmentCatalogController : ApiController
    {
        private readonly string _connectionString;
        public EquipmentCatalogController()
        {
            ConnectionStringSettings settings=ConfigurationManager.ConnectionStrings["ServiceCenterDb"];
            if(settings==null||string.IsNullOrWhiteSpace(settings.ConnectionString))throw new ConfigurationErrorsException("Не найдена строка подключения ServiceCenterDb.");
            _connectionString=settings.ConnectionString;
        }

        [HttpGet,Route("types")]
        public IHttpActionResult GetTypes(){return GetNames("SELECT EquipmentTypeId,TypeName FROM dbo.KP_EquipmentTypes ORDER BY TypeName;");}
        [HttpGet,Route("manufacturers")]
        public IHttpActionResult GetManufacturers(){return GetNames("SELECT ManufacturerId,ManufacturerName FROM dbo.KP_Manufacturers ORDER BY ManufacturerName;");}

        private IHttpActionResult GetNames(string sql)
        {
            try{using(SqlConnection cn=new SqlConnection(_connectionString))using(SqlCommand cmd=new SqlCommand(sql,cn)){cn.Open();using(SqlDataReader r=cmd.ExecuteReader()){List<object> rows=new List<object>();while(r.Read())rows.Add(new{Id=r.GetInt32(0),Name=r.GetString(1)});return Ok(rows);}}}
            catch(SqlException){return InternalServerError();}
        }

        [HttpPost,Route("types")]
        public IHttpActionResult CreateType(NameRequest request){return CreateName(request,"dbo.KP_EquipmentTypes","EquipmentTypeId","TypeName");}
        [HttpPost,Route("manufacturers")]
        public IHttpActionResult CreateManufacturer(NameRequest request){return CreateName(request,"dbo.KP_Manufacturers","ManufacturerId","ManufacturerName");}

        [HttpPut,Route("types/{id:int}")]
        public IHttpActionResult UpdateType(int id,NameRequest request){return UpdateName(id,request,"dbo.KP_EquipmentTypes","EquipmentTypeId","TypeName");}

        [HttpPut,Route("manufacturers/{id:int}")]
        public IHttpActionResult UpdateManufacturer(int id,NameRequest request){return UpdateName(id,request,"dbo.KP_Manufacturers","ManufacturerId","ManufacturerName");}

        [HttpDelete,Route("types/{id:int}")]
        public IHttpActionResult DeleteType(int id){return DeleteName(id,"dbo.KP_EquipmentTypes","EquipmentTypeId");}

        [HttpDelete,Route("manufacturers/{id:int}")]
        public IHttpActionResult DeleteManufacturer(int id){return DeleteName(id,"dbo.KP_Manufacturers","ManufacturerId");}

        private IHttpActionResult CreateName(NameRequest request,string table,string idColumn,string nameColumn)
        {
            if(request==null||!ModelState.IsValid)return BadRequest(ModelState);
            string sql="INSERT "+table+"("+nameColumn+") OUTPUT INSERTED."+idColumn+" VALUES(@Name);";
            try{using(SqlConnection cn=new SqlConnection(_connectionString))using(SqlCommand cmd=new SqlCommand(sql,cn)){cmd.Parameters.Add("@Name",SqlDbType.NVarChar,100).Value=request.Name.Trim();cn.Open();int id=(int)cmd.ExecuteScalar();return Content(HttpStatusCode.Created,new{Id=id,Name=request.Name.Trim()});}}
            catch(SqlException ex){if(ex.Number==2627||ex.Number==2601)return Content(HttpStatusCode.Conflict,new{message="Такая запись уже существует."});return InternalServerError();}
        }

        private IHttpActionResult UpdateName(int id,NameRequest request,string table,string idColumn,string nameColumn)
        {
            if(id<=0||request==null||!ModelState.IsValid)return BadRequest(ModelState);
            string sql="UPDATE "+table+" SET "+nameColumn+"=@Name WHERE "+idColumn+"=@Id;SELECT @@ROWCOUNT;";
            try{using(SqlConnection cn=new SqlConnection(_connectionString))using(SqlCommand cmd=new SqlCommand(sql,cn)){cmd.Parameters.Add("@Id",SqlDbType.Int).Value=id;cmd.Parameters.Add("@Name",SqlDbType.NVarChar,100).Value=request.Name.Trim();cn.Open();return(int)cmd.ExecuteScalar()==0?(IHttpActionResult)NotFound():Ok(new{Id=id,Name=request.Name.Trim()});}}
            catch(SqlException ex){if(ex.Number==2627||ex.Number==2601)return Content(HttpStatusCode.Conflict,new{message="Такая запись уже существует."});return InternalServerError();}
        }

        private IHttpActionResult DeleteName(int id,string table,string idColumn)
        {
            if(id<=0)return BadRequest("Идентификатор должен быть положительным числом.");
            string sql="DELETE "+table+" WHERE "+idColumn+"=@Id;SELECT @@ROWCOUNT;";
            try{using(SqlConnection cn=new SqlConnection(_connectionString))using(SqlCommand cmd=new SqlCommand(sql,cn)){cmd.Parameters.Add("@Id",SqlDbType.Int).Value=id;cn.Open();return(int)cmd.ExecuteScalar()==0?(IHttpActionResult)NotFound():StatusCode(HttpStatusCode.NoContent);}}
            catch(SqlException ex){if(ex.Number==547)return Content(HttpStatusCode.Conflict,new{message="Нельзя удалить запись, которая используется моделями техники."});return InternalServerError();}
        }

        [HttpPost,Route("models")]
        public IHttpActionResult CreateModel(EquipmentModelRequest request)
        {
            if(request==null||!ModelState.IsValid)return BadRequest(ModelState);
            const string sql=@"INSERT dbo.KP_EquipmentModels(EquipmentTypeId,ManufacturerId,ModelName) OUTPUT INSERTED.EquipmentModelId
                VALUES(@TypeId,@ManufacturerId,@ModelName);";
            try{using(SqlConnection cn=new SqlConnection(_connectionString))using(SqlCommand cmd=new SqlCommand(sql,cn)){cmd.Parameters.Add("@TypeId",SqlDbType.Int).Value=request.EquipmentTypeId;cmd.Parameters.Add("@ManufacturerId",SqlDbType.Int).Value=request.ManufacturerId;cmd.Parameters.Add("@ModelName",SqlDbType.NVarChar,100).Value=request.ModelName.Trim();cn.Open();int id=(int)cmd.ExecuteScalar();return Content(HttpStatusCode.Created,new{EquipmentModelId=id});}}
            catch(SqlException ex){if(ex.Number==2627||ex.Number==2601)return Content(HttpStatusCode.Conflict,new{message="Такая модель уже существует."});if(ex.Number==547)return BadRequest("Указаны несуществующие тип или производитель.");return InternalServerError();}
        }

        [HttpPut,Route("models/{id:int}")]
        public IHttpActionResult UpdateModel(int id,EquipmentModelRequest request)
        {
            if(id<=0||request==null||!ModelState.IsValid)return BadRequest(ModelState);
            const string sql=@"UPDATE dbo.KP_EquipmentModels SET EquipmentTypeId=@TypeId,ManufacturerId=@ManufacturerId,ModelName=@ModelName WHERE EquipmentModelId=@Id;SELECT @@ROWCOUNT;";
            try{using(SqlConnection cn=new SqlConnection(_connectionString))using(SqlCommand cmd=new SqlCommand(sql,cn)){cmd.Parameters.Add("@Id",SqlDbType.Int).Value=id;cmd.Parameters.Add("@TypeId",SqlDbType.Int).Value=request.EquipmentTypeId;cmd.Parameters.Add("@ManufacturerId",SqlDbType.Int).Value=request.ManufacturerId;cmd.Parameters.Add("@ModelName",SqlDbType.NVarChar,100).Value=request.ModelName.Trim();cn.Open();return(int)cmd.ExecuteScalar()==0?(IHttpActionResult)NotFound():Ok(new{EquipmentModelId=id});}}
            catch(SqlException ex){if(ex.Number==2627||ex.Number==2601)return Content(HttpStatusCode.Conflict,new{message="Такая модель уже существует."});if(ex.Number==547)return BadRequest("Указаны несуществующие тип или производитель.");return InternalServerError();}
        }

        [HttpDelete,Route("models/{id:int}")]
        public IHttpActionResult DeleteModel(int id)
        {
            if(id<=0)return BadRequest("Идентификатор должен быть положительным числом.");
            const string sql="DELETE dbo.KP_EquipmentModels WHERE EquipmentModelId=@Id;SELECT @@ROWCOUNT;";
            try{using(SqlConnection cn=new SqlConnection(_connectionString))using(SqlCommand cmd=new SqlCommand(sql,cn)){cmd.Parameters.Add("@Id",SqlDbType.Int).Value=id;cn.Open();return(int)cmd.ExecuteScalar()==0?(IHttpActionResult)NotFound():StatusCode(HttpStatusCode.NoContent);}}
            catch(SqlException ex){if(ex.Number==547)return Content(HttpStatusCode.Conflict,new{message="Нельзя удалить модель, которая используется зарегистрированной техникой."});return InternalServerError();}
        }
    }
}
