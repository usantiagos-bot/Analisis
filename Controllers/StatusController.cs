using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.Http;

namespace ProyectoAnalisis.Controllers
{
    [RoutePrefix("StatusUsuarios")]
    public class StatusUsuariosController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;

        // DTO
        public class StatusUsuarioDto
        {
            public int IdStatusUsuario { get; set; }
            public string Nombre { get; set; }
            public DateTime FechaCreacion { get; set; }
            public string UsuarioCreacion { get; set; }
            public DateTime? FechaModificacion { get; set; }
            public string UsuarioModificacion { get; set; }
        }

        private static string F(DateTime? d) => d.HasValue ? d.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null;

        // GET /StatusUsuarios/Listar?IdStatusUsuario=&BuscarNombre=&Page=1&PageSize=50
        [HttpGet]
        [Route("Listar")]
        public IHttpActionResult Listar(int? IdStatusUsuario = null, string BuscarNombre = null, int Page = 1, int PageSize = 50)
        {
            try
            {
                var lista = new List<StatusUsuarioDto>();

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_StatusUsuario_Listar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdStatusUsuario", SqlDbType.Int).Value = (object)IdStatusUsuario ?? DBNull.Value;
                    cmd.Parameters.Add("@BuscarNombre", SqlDbType.VarChar, 100).Value = (object)BuscarNombre ?? DBNull.Value;
                    cmd.Parameters.Add("@Page", SqlDbType.Int).Value = Page;
                    cmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = PageSize;

                    cn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            lista.Add(new StatusUsuarioDto
                            {
                                IdStatusUsuario = Convert.ToInt32(rd["IdStatusUsuario"]),
                                Nombre = rd["Nombre"] as string,
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
                    s.IdStatusUsuario,
                    s.Nombre,
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

        // GET /StatusUsuarios/Crear?Nombre=&Usuario=
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
                using (var cmd = new SqlCommand("dbo.sp_StatusUsuario_Crear", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 100).Value = Nombre;
                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = Usuario;

                    cn.Open();
                    var scalar = cmd.ExecuteScalar();
                    nuevoId = Convert.ToInt32(scalar);
                }

                var status = ObtenerPorId(nuevoId);
                if (status == null) return Ok(new { ok = false, error = "No se pudo leer el registro recién creado." });

                var data = new
                {
                    status.IdStatusUsuario,
                    status.Nombre,
                    FechaCreacion = F(status.FechaCreacion),
                    FechaModificacion = F(status.FechaModificacion),
                    status.UsuarioCreacion,
                    status.UsuarioModificacion
                };

                return Ok(new { ok = true, data });
            }
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error interno: " + e.Message));
            }
        }

        // GET /StatusUsuarios/Actualizar?IdStatusUsuario=&Nombre=&Usuario=
        [HttpGet]
        [Route("Actualizar")]
        public IHttpActionResult Actualizar(int IdStatusUsuario, string Nombre = null, string Usuario = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario))
                    return Ok(new { ok = false, error = "Usuario es requerido." });

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_StatusUsuario_Actualizar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdStatusUsuario", SqlDbType.Int).Value = IdStatusUsuario;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 100).Value =
                        string.IsNullOrWhiteSpace(Nombre) ? (object)DBNull.Value : Nombre;
                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = Usuario;

                    cn.Open();
                    cmd.ExecuteNonQuery();
                }

                var status = ObtenerPorId(IdStatusUsuario);
                if (status == null) return Ok(new { ok = false, error = "No encontrado" });

                var data = new
                {
                    status.IdStatusUsuario,
                    status.Nombre,
                    FechaCreacion = F(status.FechaCreacion),
                    FechaModificacion = F(status.FechaModificacion),
                    status.UsuarioCreacion,
                    status.UsuarioModificacion
                };

                return Ok(new { ok = true, data });
            }
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error interno: " + e.Message));
            }
        }

        // GET /StatusUsuarios/Eliminar?IdStatusUsuario=
        [HttpGet]
        [Route("Eliminar")]
        public IHttpActionResult Eliminar(int IdStatusUsuario)
        {
            try
            {
                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_StatusUsuario_Eliminar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdStatusUsuario", SqlDbType.Int).Value = IdStatusUsuario;

                    cn.Open();
                    cmd.ExecuteNonQuery();
                }

                return Ok(new { ok = true, message = "Eliminado correctamente", id = IdStatusUsuario });
            }
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error interno: " + e.Message));
            }
        }

        // -------- Helpers --------
        private StatusUsuarioDto ObtenerPorId(int id)
        {
            using (var cn = new SqlConnection(Cnx))
            using (var cmd = new SqlCommand("dbo.sp_StatusUsuario_Listar", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@IdStatusUsuario", SqlDbType.Int).Value = id;
                cmd.Parameters.Add("@BuscarNombre", SqlDbType.VarChar, 100).Value = DBNull.Value;
                cmd.Parameters.Add("@Page", SqlDbType.Int).Value = 1;
                cmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = 1;

                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read()) return null;

                    return new StatusUsuarioDto
                    {
                        IdStatusUsuario = Convert.ToInt32(rd["IdStatusUsuario"]),
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