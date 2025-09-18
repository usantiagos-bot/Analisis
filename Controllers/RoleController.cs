using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.Http;

namespace ProyectoAnalisis.Controllers
{
    [RoutePrefix("Roles")]
    public class RolesController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;

        // DTO
        public class RolDto
        {
            public int IdRole { get; set; }
            public string Nombre { get; set; }
            public DateTime FechaCreacion { get; set; }
            public string UsuarioCreacion { get; set; }
            public DateTime? FechaModificacion { get; set; }
            public string UsuarioModificacion { get; set; }
        }

        private static string F(DateTime? d) => d.HasValue ? d.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null;

        // GET /Roles/Listar?IdRole=&BuscarNombre=&Page=1&PageSize=50
        [HttpGet]
        [Route("Listar")]
        public IHttpActionResult Listar(int? IdRole = null, string BuscarNombre = null, int Page = 1, int PageSize = 50)
        {
            try
            {
                var lista = new List<RolDto>();

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Role_Listar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdRole", SqlDbType.Int).Value = (object)IdRole ?? DBNull.Value;
                    cmd.Parameters.Add("@BuscarNombre", SqlDbType.VarChar, 100).Value = (object)BuscarNombre ?? DBNull.Value;
                    cmd.Parameters.Add("@Page", SqlDbType.Int).Value = Page;
                    cmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = PageSize;

                    cn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            lista.Add(new RolDto
                            {
                                IdRole = Convert.ToInt32(rd["IdRole"]),
                                Nombre = rd["Nombre"] as string,
                                FechaCreacion = Convert.ToDateTime(rd["FechaCreacion"]),
                                UsuarioCreacion = rd["UsuarioCreacion"] as string,
                                FechaModificacion = rd["FechaModificacion"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["FechaModificacion"]),
                                UsuarioModificacion = rd["UsuarioModificacion"] as string
                            });
                        }
                    }
                }

                var data = lista.ConvertAll(r => new
                {
                    r.IdRole,
                    r.Nombre,
                    FechaCreacion = F(r.FechaCreacion),
                    FechaModificacion = F(r.FechaModificacion),
                    r.UsuarioCreacion,
                    r.UsuarioModificacion
                });

                return Ok(new { ok = true, data });
            }
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error interno: " + e.Message));
            }
        }

        // GET /Roles/Crear?Nombre=&Usuario=
        [HttpGet]
        [Route("Crear")]
        public IHttpActionResult Crear(string Nombre, string Usuario)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario))
                    return Ok(new { ok = false, error = "Usuario es requerido." });

                int nuevoId;

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Role_Crear", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 50).Value = Nombre;
                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = Usuario;

                    cn.Open();
                    var scalar = cmd.ExecuteScalar();
                    nuevoId = Convert.ToInt32(scalar);
                }

                var rol = ObtenerPorId(nuevoId);
                if (rol == null) return Ok(new { ok = false, error = "No se pudo leer el registro recién creado." });

                var data = new
                {
                    rol.IdRole,
                    rol.Nombre,
                    FechaCreacion = F(rol.FechaCreacion),
                    FechaModificacion = F(rol.FechaModificacion),
                    rol.UsuarioCreacion,
                    rol.UsuarioModificacion
                };

                return Ok(new { ok = true, data });
            }
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error interno: " + e.Message));
            }
        }

        // GET /Roles/Actualizar?IdRole=&Nombre=&Usuario=
        [HttpGet]
        [Route("Actualizar")]
        public IHttpActionResult Actualizar(int IdRole, string Nombre = null, string Usuario = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario))
                    return Ok(new { ok = false, error = "Usuario es requerido." });

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Role_Actualizar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdRole", SqlDbType.Int).Value = IdRole;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 50).Value =
                        string.IsNullOrWhiteSpace(Nombre) ? (object)DBNull.Value : Nombre;
                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = Usuario;

                    cn.Open();
                    cmd.ExecuteNonQuery();
                }

                var rol = ObtenerPorId(IdRole);
                if (rol == null) return Ok(new { ok = false, error = "No encontrado" });

                var data = new
                {
                    rol.IdRole,
                    rol.Nombre,
                    FechaCreacion = F(rol.FechaCreacion),
                    FechaModificacion = F(rol.FechaModificacion),
                    rol.UsuarioCreacion,
                    rol.UsuarioModificacion
                };

                return Ok(new { ok = true, data });
            }
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error interno: " + e.Message));
            }
        }

        // GET /Roles/Eliminar?IdRole=
        [HttpGet]
        [Route("Eliminar")]
        public IHttpActionResult Eliminar(int IdRole)
        {
            try
            {
                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Role_Eliminar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdRole", SqlDbType.Int).Value = IdRole;

                    cn.Open();
                    cmd.ExecuteNonQuery();
                }

                return Ok(new { ok = true, message = "Eliminado correctamente", id = IdRole });
            }
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error interno: " + e.Message));
            }
        }

        // -------- Helpers --------
        private RolDto ObtenerPorId(int id)
        {
            using (var cn = new SqlConnection(Cnx))
            using (var cmd = new SqlCommand("dbo.sp_Role_Listar", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@IdRole", SqlDbType.Int).Value = id;
                cmd.Parameters.Add("@BuscarNombre", SqlDbType.VarChar, 100).Value = DBNull.Value;
                cmd.Parameters.Add("@Page", SqlDbType.Int).Value = 1;
                cmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = 1;

                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read()) return null;

                    return new RolDto
                    {
                        IdRole = Convert.ToInt32(rd["IdRole"]),
                        Nombre = rd["Nombre"] as string,
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
