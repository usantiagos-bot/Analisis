using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.Http;

namespace ProyectoAnalisis.Controllers
{
    [RoutePrefix("Generos")]
    public class GenerosController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;

        // DTO
        public class GeneroDto
        {
            public int IdGenero { get; set; }
            public string Nombre { get; set; }
            public DateTime FechaCreacion { get; set; }
            public string UsuarioCreacion { get; set; }
            public DateTime? FechaModificacion { get; set; }
            public string UsuarioModificacion { get; set; }
        }

        private static string F(DateTime? d) => d.HasValue ? d.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null;

        // GET /Generos/Listar?IdGenero=&BuscarNombre=&Page=1&PageSize=50
        [HttpGet]
        [Route("Listar")]
        public IHttpActionResult Listar(int? IdGenero = null, string BuscarNombre = null, int Page = 1, int PageSize = 50)
        {
            try
            {
                var lista = new List<GeneroDto>();

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Genero_Listar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdGenero", SqlDbType.Int).Value = (object)IdGenero ?? DBNull.Value;
                    cmd.Parameters.Add("@BuscarNombre", SqlDbType.VarChar, 100).Value = (object)BuscarNombre ?? DBNull.Value;
                    cmd.Parameters.Add("@Page", SqlDbType.Int).Value = Page;
                    cmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = PageSize;

                    cn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            lista.Add(new GeneroDto
                            {
                                IdGenero = Convert.ToInt32(rd["IdGenero"]),
                                Nombre = rd["Nombre"] as string,
                                FechaCreacion = Convert.ToDateTime(rd["FechaCreacion"]),
                                UsuarioCreacion = rd["UsuarioCreacion"] as string,
                                FechaModificacion = rd["FechaModificacion"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["FechaModificacion"]),
                                UsuarioModificacion = rd["UsuarioModificacion"] as string
                            });
                        }
                    }
                }

                var data = lista.ConvertAll(g => new
                {
                    g.IdGenero,
                    g.Nombre,
                    FechaCreacion = F(g.FechaCreacion),
                    FechaModificacion = F(g.FechaModificacion),
                    g.UsuarioCreacion,
                    g.UsuarioModificacion
                });

                return Ok(new { ok = true, data });
            }
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error interno: " + e.Message));
            }
        }

        // GET /Generos/Crear?Nombre=&Usuario=
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
                using (var cmd = new SqlCommand("dbo.sp_Genero_Crear", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 100).Value = Nombre;
                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = Usuario;

                    cn.Open();
                    var scalar = cmd.ExecuteScalar();
                    nuevoId = Convert.ToInt32(scalar);
                }

                var genero = ObtenerPorId(nuevoId);
                if (genero == null) return Ok(new { ok = false, error = "No se pudo leer el registro recién creado." });

                var data = new
                {
                    genero.IdGenero,
                    genero.Nombre,
                    FechaCreacion = F(genero.FechaCreacion),
                    FechaModificacion = F(genero.FechaModificacion),
                    genero.UsuarioCreacion,
                    genero.UsuarioModificacion
                };

                return Ok(new { ok = true, data });
            }
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error interno: " + e.Message));
            }
        }

        // GET /Generos/Actualizar?IdGenero=&Nombre=&Usuario=
        [HttpGet]
        [Route("Actualizar")]
        public IHttpActionResult Actualizar(int IdGenero, string Nombre = null, string Usuario = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario))
                    return Ok(new { ok = false, error = "Usuario es requerido." });

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Genero_Actualizar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdGenero", SqlDbType.Int).Value = IdGenero;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 100).Value =
                        string.IsNullOrWhiteSpace(Nombre) ? (object)DBNull.Value : Nombre;
                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = Usuario;

                    cn.Open();
                    cmd.ExecuteNonQuery();
                }

                var genero = ObtenerPorId(IdGenero);
                if (genero == null) return Ok(new { ok = false, error = "No encontrado" });

                var data = new
                {
                    genero.IdGenero,
                    genero.Nombre,
                    FechaCreacion = F(genero.FechaCreacion),
                    FechaModificacion = F(genero.FechaModificacion),
                    genero.UsuarioCreacion,
                    genero.UsuarioModificacion
                };

                return Ok(new { ok = true, data });
            }
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error interno: " + e.Message));
            }
        }

        // GET /Generos/Eliminar?IdGenero=
        [HttpGet]
        [Route("Eliminar")]
        public IHttpActionResult Eliminar(int IdGenero)
        {
            try
            {
                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Genero_Eliminar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdGenero", SqlDbType.Int).Value = IdGenero;

                    cn.Open();
                    cmd.ExecuteNonQuery();
                }

                return Ok(new { ok = true, message = "Eliminado correctamente", id = IdGenero });
            }
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error interno: " + e.Message));
            }
        }

        // -------- Helpers --------
        private GeneroDto ObtenerPorId(int id)
        {
            using (var cn = new SqlConnection(Cnx))
            using (var cmd = new SqlCommand("dbo.sp_Genero_Listar", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@IdGenero", SqlDbType.Int).Value = id;
                cmd.Parameters.Add("@BuscarNombre", SqlDbType.VarChar, 100).Value = DBNull.Value;
                cmd.Parameters.Add("@Page", SqlDbType.Int).Value = 1;
                cmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = 1;

                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read()) return null;

                    return new GeneroDto
                    {
                        IdGenero = Convert.ToInt32(rd["IdGenero"]),
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