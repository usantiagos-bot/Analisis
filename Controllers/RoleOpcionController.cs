using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.Http;

namespace ProyectoAnalisis.Controllers
{
    [RoutePrefix("RoleOpciones")]
    public class RoleOpcionController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;

        // DTO para permisos
        public class RoleOpcionDto
        {
            public int IdRole { get; set; }
            public int IdOpcion { get; set; }
            public string NombreOpcion { get; set; }
            public bool Alta { get; set; }
            public bool Baja { get; set; }
            public bool Cambio { get; set; }
            public bool Imprimir { get; set; }
            public bool Exportar { get; set; }
            public DateTime FechaCreacion { get; set; }
            public string UsuarioCreacion { get; set; }
            public DateTime? FechaModificacion { get; set; }
            public string UsuarioModificacion { get; set; }
        }

        // GET /RoleOpciones/Listar?IdRole=1
        [HttpGet]
        [Route("Listar")]
        public IHttpActionResult Listar(int IdRole)
        {
            try
            {
                var lista = new List<RoleOpcionDto>();

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("sp_RoleOpcion_Listar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdRole", SqlDbType.Int).Value = IdRole;

                    cn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            lista.Add(new RoleOpcionDto
                            {
                                IdRole = Convert.ToInt32(rd["IdRole"]),
                                IdOpcion = Convert.ToInt32(rd["IdOpcion"]),
                                NombreOpcion = rd["NombreOpcion"] as string,
                                Alta = Convert.ToBoolean(rd["Alta"]),
                                Baja = Convert.ToBoolean(rd["Baja"]),
                                Cambio = Convert.ToBoolean(rd["Cambio"]),
                                Imprimir = Convert.ToBoolean(rd["Imprimir"]),
                                Exportar = Convert.ToBoolean(rd["Exportar"]),
                                FechaCreacion = Convert.ToDateTime(rd["FechaCreacion"]),
                                UsuarioCreacion = rd["UsuarioCreacion"] as string,
                                FechaModificacion = rd["FechaModificacion"] == DBNull.Value ?
                                    (DateTime?)null : Convert.ToDateTime(rd["FechaModificacion"]),
                                UsuarioModificacion = rd["UsuarioModificacion"] as string
                            });
                        }
                    }
                }

                return Ok(new { ok = true, data = lista });
            }
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error al listar permisos: " + e.Message));
            }
        }

        // POST /RoleOpciones/Guardar
        [HttpPost]
        [Route("Guardar")]
        public IHttpActionResult Guardar([FromBody] RoleOpcionDto permiso)
        {
            try
            {
                if (permiso == null)
                    return BadRequest("Datos de permiso requeridos");

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("sp_RoleOpcion_Guardar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdRole", SqlDbType.Int).Value = permiso.IdRole;
                    cmd.Parameters.Add("@IdOpcion", SqlDbType.Int).Value = permiso.IdOpcion;
                    cmd.Parameters.Add("@Alta", SqlDbType.Bit).Value = permiso.Alta;
                    cmd.Parameters.Add("@Baja", SqlDbType.Bit).Value = permiso.Baja;
                    cmd.Parameters.Add("@Cambio", SqlDbType.Bit).Value = permiso.Cambio;
                    cmd.Parameters.Add("@Imprimir", SqlDbType.Bit).Value = permiso.Imprimir;
                    cmd.Parameters.Add("@Exportar", SqlDbType.Bit).Value = permiso.Exportar;
                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value =
                        permiso.UsuarioModificacion ?? permiso.UsuarioCreacion ?? "system";

                    cn.Open();
                    cmd.ExecuteNonQuery();
                }

                return Ok(new { ok = true, message = "Permisos guardados correctamente" });
            }
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error al guardar permisos: " + e.Message));
            }
        }

        // POST /RoleOpciones/GuardarMultiple
        [HttpPost]
        [Route("GuardarMultiple")]
        public IHttpActionResult GuardarMultiple([FromBody] List<RoleOpcionDto> permisos)
        {
            try
            {
                if (permisos == null || permisos.Count == 0)
                    return BadRequest("Datos de permisos requeridos");

                using (var cn = new SqlConnection(Cnx))
                {
                    cn.Open();

                    foreach (var permiso in permisos)
                    {
                        using (var cmd = new SqlCommand("sp_RoleOpcion_Guardar", cn))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.Parameters.Add("@IdRole", SqlDbType.Int).Value = permiso.IdRole;
                            cmd.Parameters.Add("@IdOpcion", SqlDbType.Int).Value = permiso.IdOpcion;
                            cmd.Parameters.Add("@Alta", SqlDbType.Bit).Value = permiso.Alta;
                            cmd.Parameters.Add("@Baja", SqlDbType.Bit).Value = permiso.Baja;
                            cmd.Parameters.Add("@Cambio", SqlDbType.Bit).Value = permiso.Cambio;
                            cmd.Parameters.Add("@Imprimir", SqlDbType.Bit).Value = permiso.Imprimir;
                            cmd.Parameters.Add("@Exportar", SqlDbType.Bit).Value = permiso.Exportar;
                            cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value =
                                permiso.UsuarioModificacion ?? permiso.UsuarioCreacion ?? "system";

                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                return Ok(new { ok = true, message = "Permisos guardados correctamente" });
            }
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error al guardar permisos: " + e.Message));
            }
        }
    }
}