using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.Http;

namespace ProyectoAnalisis.Controllers
{
    [RoutePrefix("Sucursales")]
    public class SucursalesController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;

        // DTO
        public class SucursalDto
        {
            public int IdSucursal { get; set; }
            public string Nombre { get; set; }
            public string Direccion { get; set; }
            public int IdEmpresa { get; set; }
            public DateTime FechaCreacion { get; set; }
            public string UsuarioCreacion { get; set; }
            public DateTime? FechaModificacion { get; set; }
            public string UsuarioModificacion { get; set; }
        }

        private static string F(DateTime? d) => d.HasValue ? d.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null;

        // GET /Sucursales/Listar?IdSucursal=&IdEmpresa=&BuscarNombre=&Page=1&PageSize=50
        [HttpGet]
        [Route("Listar")]
        public IHttpActionResult Listar(int? IdSucursal = null, int? IdEmpresa = null,
                                        string BuscarNombre = null, int Page = 1, int PageSize = 50)
        {
            try
            {
                var lista = new List<SucursalDto>();

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Sucursal_Listar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdSucursal", SqlDbType.Int).Value = (object)IdSucursal ?? DBNull.Value;
                    cmd.Parameters.Add("@IdEmpresa", SqlDbType.Int).Value = (object)IdEmpresa ?? DBNull.Value;
                    cmd.Parameters.Add("@BuscarNombre", SqlDbType.VarChar, 100).Value = (object)BuscarNombre ?? DBNull.Value;
                    cmd.Parameters.Add("@Page", SqlDbType.Int).Value = Page;
                    cmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = PageSize;

                    cn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            lista.Add(new SucursalDto
                            {
                                IdSucursal = Convert.ToInt32(rd["IdSucursal"]),
                                Nombre = rd["Nombre"] as string,
                                Direccion = rd["Direccion"] as string,
                                IdEmpresa = Convert.ToInt32(rd["IdEmpresa"]),
                                FechaCreacion = Convert.ToDateTime(rd["FechaCreacion"]),
                                UsuarioCreacion = rd["UsuarioCreacion"] as string,
                                FechaModificacion = rd["FechaModificacion"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["FechaModificacion"]),
                                UsuarioModificacion = rd["UsuarioModificacion"] as string
                            });
                        }
                    }
                }

                var data = lista.ConvertAll(s => new
                {
                    s.IdSucursal,
                    s.Nombre,
                    s.Direccion,
                    s.IdEmpresa,
                    FechaCreacion = F(s.FechaCreacion),
                    FechaModificacion = F(s.FechaModificacion),
                    s.UsuarioCreacion,
                    s.UsuarioModificacion
                });

                return Ok(new { ok = true, data });
            }
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error interno: " + e.Message));
            }
        }

        // GET /Sucursales/Crear?Nombre=&Direccion=&IdEmpresa=&Usuario=
        [HttpGet]
        [Route("Crear")]
        public IHttpActionResult Crear(string Nombre, string Direccion, int IdEmpresa, string Usuario)
        {
            try
            {
                int nuevoId;

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Sucursal_Crear", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 100).Value = Nombre;
                    cmd.Parameters.Add("@Direccion", SqlDbType.VarChar, 200).Value = Direccion;
                    cmd.Parameters.Add("@IdEmpresa", SqlDbType.Int).Value = IdEmpresa;
                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = Usuario;

                    cn.Open();
                    // Tu SP devuelve un resultset con IdSucursal; usamos ExecuteScalar para leer la 1ª columna de la 1ª fila.
                    var scalar = cmd.ExecuteScalar();
                    nuevoId = Convert.ToInt32(scalar);
                }

                var suc = ObtenerPorId(nuevoId);
                if (suc == null) return Ok(new { ok = false, error = "No se pudo leer el registro recién creado." });

                var data = new
                {
                    suc.IdSucursal,
                    suc.Nombre,
                    suc.Direccion,
                    suc.IdEmpresa,
                    FechaCreacion = F(suc.FechaCreacion),
                    FechaModificacion = F(suc.FechaModificacion),
                    suc.UsuarioCreacion,
                    suc.UsuarioModificacion
                };

                return Ok(new { ok = true, data });
            }
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error interno: " + e.Message));
            }
        }

        // GET /Sucursales/Actualizar?IdSucursal=&Nombre=&Direccion=&IdEmpresa=&Usuario=
        // GET /Sucursales/Actualizar?IdSucursal=1&Usuario=admin
        // GET /Sucursales/Actualizar?IdSucursal=1&Nombre=Nueva&Usuario=admin
        // GET /Sucursales/Actualizar?IdSucursal=1&Direccion=Calle%20123&Usuario=admin
        // GET /Sucursales/Actualizar?IdSucursal=1&IdEmpresa=5&Usuario=admin
        [HttpGet]
        [Route("Actualizar")]
        public IHttpActionResult Actualizar(
            int IdSucursal,
            string Nombre = null,
            string Direccion = null,
            int? IdEmpresa = null,
            string Usuario = null)
        {
            try
            {
                // Si quieres forzar Usuario requerido, valida aquí:
                if (string.IsNullOrWhiteSpace(Usuario))
                    return Ok(new { ok = false, error = "Usuario es requerido." });

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Sucursal_Actualizar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("@IdSucursal", SqlDbType.Int).Value = IdSucursal;

                    // Si el cliente no lo envía o lo envía vacío, va como NULL
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 100).Value =
                        string.IsNullOrWhiteSpace(Nombre) ? (object)DBNull.Value : Nombre;

                    cmd.Parameters.Add("@Direccion", SqlDbType.VarChar, 200).Value =
                        string.IsNullOrWhiteSpace(Direccion) ? (object)DBNull.Value : Direccion;

                    cmd.Parameters.Add("@IdEmpresa", SqlDbType.Int).Value =
                        (object)IdEmpresa ?? DBNull.Value;

                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value =
                        (object)Usuario ?? DBNull.Value;

                    cn.Open();
                    cmd.ExecuteNonQuery();
                }

                var suc = ObtenerPorId(IdSucursal);
                if (suc == null) return Ok(new { ok = false, error = "No encontrado" });

                var data = new
                {
                    suc.IdSucursal,
                    suc.Nombre,
                    suc.Direccion,
                    suc.IdEmpresa,
                    FechaCreacion = F(suc.FechaCreacion),
                    FechaModificacion = F(suc.FechaModificacion),
                    suc.UsuarioCreacion,
                    suc.UsuarioModificacion
                };

                return Ok(new { ok = true, data });
            }
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error interno: " + e.Message));
            }
        }


        // GET /Sucursales/Eliminar?IdSucursal=
        [HttpGet]
        [Route("Eliminar")]
        public IHttpActionResult Eliminar(int IdSucursal)
        {
            try
            {
                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Sucursal_Eliminar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdSucursal", SqlDbType.Int).Value = IdSucursal;

                    cn.Open();
                    cmd.ExecuteNonQuery();
                }

                return Ok(new { ok = true, message = "Eliminado correctamente", id = IdSucursal });
            }
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error interno: " + e.Message));
            }
        }

        // -------- Helpers --------
        private SucursalDto ObtenerPorId(int id)
        {
            using (var cn = new SqlConnection(Cnx))
            using (var cmd = new SqlCommand("dbo.sp_Sucursal_Listar", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@IdSucursal", SqlDbType.Int).Value = id;
                cmd.Parameters.Add("@IdEmpresa", SqlDbType.Int).Value = DBNull.Value;
                cmd.Parameters.Add("@BuscarNombre", SqlDbType.VarChar, 100).Value = DBNull.Value;
                cmd.Parameters.Add("@Page", SqlDbType.Int).Value = 1;
                cmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = 1;

                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read()) return null;

                    return new SucursalDto
                    {
                        IdSucursal = Convert.ToInt32(rd["IdSucursal"]),
                        Nombre = rd["Nombre"] as string,
                        Direccion = rd["Direccion"] as string,
                        IdEmpresa = Convert.ToInt32(rd["IdEmpresa"]),
                        FechaCreacion = Convert.ToDateTime(rd["FechaCreacion"]),
                        UsuarioCreacion = rd["UsuarioCreacion"] as string,
                        FechaModificacion = rd["FechaModificacion"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["FechaModificacion"]),
                        UsuarioModificacion = rd["UsuarioModificacion"] as string
                    };
                }
            }
        }
    }
}
