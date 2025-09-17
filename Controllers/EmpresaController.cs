using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Web.Http;
using ProyectoAnalisis.Helpers;
using ProyectoAnalisis.Permissions;

namespace ProyectoAnalisis.Controllers
{
    [RoutePrefix("Empresas")]
    public class EmpresasController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;

        // ---------- DTO ----------
        public class EmpresaDto
        {
            public int IdEmpresa { get; set; }
            public string Nombre { get; set; }
            public string Direccion { get; set; }
            public string Nit { get; set; }

            public int? PasswordCantidadMayusculas { get; set; }
            public int? PasswordCantidadMinusculas { get; set; }
            public int? PasswordCantidadCaracteresEspeciales { get; set; }
            public int? PasswordCantidadCaducidadDias { get; set; }
            public int? PasswordLargo { get; set; }
            public int? PasswordIntentosAntesDeBloquear { get; set; }
            public int? PasswordCantidadNumeros { get; set; }
            public int? PasswordCantidadPreguntasValidar { get; set; }

            public DateTime FechaCreacion { get; set; }
            public string UsuarioCreacion { get; set; }
            public DateTime? FechaModificacion { get; set; }
            public string UsuarioModificacion { get; set; }
        }

        // Formateador de fechas
        private static string F(DateTime? d) => d.HasValue ? d.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null;

        // Respuesta uniforme para permiso denegado
        private IHttpActionResult Denegado(PermisoAccion acc)
            => Ok(new { ok = false, error = $"Permiso denegado ({acc})." });

        // ---------- LISTAR ----------
        // GET /Empresas/Listar?IdEmpresa=&BuscarNombre=&Nit=&Page=1&PageSize=50
        [HttpGet]
        [Route("Listar")]
        public IHttpActionResult Listar(int? IdEmpresa = null, string BuscarNombre = null, string Nit = null,
                                        int Page = 1, int PageSize = 50)
        {
            try
            {
                var lista = new List<EmpresaDto>();

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Empresa_Listar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdEmpresa", SqlDbType.Int).Value = (object)IdEmpresa ?? DBNull.Value;
                    cmd.Parameters.Add("@BuscarNombre", SqlDbType.VarChar, 100).Value = (object)BuscarNombre ?? DBNull.Value;
                    cmd.Parameters.Add("@Nit", SqlDbType.VarChar, 20).Value = (object)Nit ?? DBNull.Value;
                    cmd.Parameters.Add("@Page", SqlDbType.Int).Value = Page;
                    cmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = PageSize;

                    cn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            lista.Add(new EmpresaDto
                            {
                                IdEmpresa = Convert.ToInt32(rd["IdEmpresa"]),
                                Nombre = rd["Nombre"] as string,
                                Direccion = rd["Direccion"] as string,
                                Nit = rd["Nit"] as string,
                                PasswordCantidadMayusculas = rd["PasswordCantidadMayusculas"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["PasswordCantidadMayusculas"]),
                                PasswordCantidadMinusculas = rd["PasswordCantidadMinusculas"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["PasswordCantidadMinusculas"]),
                                PasswordCantidadCaracteresEspeciales = rd["PasswordCantidadCaracteresEspeciales"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["PasswordCantidadCaracteresEspeciales"]),
                                PasswordCantidadCaducidadDias = rd["PasswordCantidadCaducidadDias"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["PasswordCantidadCaducidadDias"]),
                                PasswordLargo = rd["PasswordLargo"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["PasswordLargo"]),
                                PasswordIntentosAntesDeBloquear = rd["PasswordIntentosAntesDeBloquear"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["PasswordIntentosAntesDeBloquear"]),
                                PasswordCantidadNumeros = rd["PasswordCantidadNumeros"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["PasswordCantidadNumeros"]),
                                PasswordCantidadPreguntasValidar = rd["PasswordCantidadPreguntasValidar"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["PasswordCantidadPreguntasValidar"]),
                                FechaCreacion = Convert.ToDateTime(rd["FechaCreacion"]),
                                UsuarioCreacion = rd["UsuarioCreacion"] as string,
                                FechaModificacion = rd["FechaModificacion"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["FechaModificacion"]),
                                UsuarioModificacion = rd["UsuarioModificacion"] as string
                            });
                        }
                    }
                }

                var data = lista.ConvertAll(e => new
                {
                    e.IdEmpresa,
                    e.Nombre,
                    e.Direccion,
                    e.Nit,
                    e.PasswordCantidadMayusculas,
                    e.PasswordCantidadMinusculas,
                    e.PasswordCantidadCaracteresEspeciales,
                    e.PasswordCantidadCaducidadDias,
                    e.PasswordLargo,
                    e.PasswordIntentosAntesDeBloquear,
                    e.PasswordCantidadNumeros,
                    e.PasswordCantidadPreguntasValidar,
                    FechaCreacion = F(e.FechaCreacion),
                    FechaModificacion = F(e.FechaModificacion),
                    e.UsuarioCreacion,
                    e.UsuarioModificacion
                });

                return Ok(new { ok = true, data });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error interno: " + ex.Message));
            }
        }

        // ---------- CREAR ----------
        // GET /Empresas/Crear?Nombre=&Direccion=&Nit=&Usuario=&PasswordLargo=&PasswordCantidadMayusculas=...
        [HttpGet]
        [Route("Crear")]
        public async Task<IHttpActionResult> Crear(
            string Nombre,
            string Direccion,
            string Nit,
            string Usuario,
            int? PasswordCantidadMayusculas = null,
            int? PasswordCantidadMinusculas = null,
            int? PasswordCantidadCaracteresEspeciales = null,
            int? PasswordCantidadCaducidadDias = null,
            int? PasswordLargo = null,
            int? PasswordIntentosAntesDeBloquear = null,
            int? PasswordCantidadNumeros = null,
            int? PasswordCantidadPreguntasValidar = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario))
                    return Ok(new { ok = false, error = "Usuario es requerido." });

                // Permiso: Alta sobre Empresas
                if (!await SeguridadHelper.TienePermisoAsync(Usuario, Opciones.Empresas, PermisoAccion.Alta))
                    return Denegado(PermisoAccion.Alta);

                int nuevoId;

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Empresa_Crear", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 100).Value = Nombre;
                    cmd.Parameters.Add("@Direccion", SqlDbType.VarChar, 200).Value = Direccion;
                    cmd.Parameters.Add("@Nit", SqlDbType.VarChar, 20).Value = Nit;
                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = Usuario;

                    cmd.Parameters.Add("@PasswordCantidadMayusculas", SqlDbType.Int).Value = (object)PasswordCantidadMayusculas ?? DBNull.Value;
                    cmd.Parameters.Add("@PasswordCantidadMinusculas", SqlDbType.Int).Value = (object)PasswordCantidadMinusculas ?? DBNull.Value;
                    cmd.Parameters.Add("@PasswordCantidadCaracteresEspeciales", SqlDbType.Int).Value = (object)PasswordCantidadCaracteresEspeciales ?? DBNull.Value;
                    cmd.Parameters.Add("@PasswordCantidadCaducidadDias", SqlDbType.Int).Value = (object)PasswordCantidadCaducidadDias ?? DBNull.Value;
                    cmd.Parameters.Add("@PasswordLargo", SqlDbType.Int).Value = (object)PasswordLargo ?? DBNull.Value;
                    cmd.Parameters.Add("@PasswordIntentosAntesDeBloquear", SqlDbType.Int).Value = (object)PasswordIntentosAntesDeBloquear ?? DBNull.Value;
                    cmd.Parameters.Add("@PasswordCantidadNumeros", SqlDbType.Int).Value = (object)PasswordCantidadNumeros ?? DBNull.Value;
                    cmd.Parameters.Add("@PasswordCantidadPreguntasValidar", SqlDbType.Int).Value = (object)PasswordCantidadPreguntasValidar ?? DBNull.Value;

                    cn.Open();
                    var scalar = cmd.ExecuteScalar();
                    nuevoId = Convert.ToInt32(scalar);
                }

                var emp = ObtenerPorId(nuevoId);
                if (emp == null) return Ok(new { ok = false, error = "No se pudo leer el registro recién creado." });

                var data = new
                {
                    emp.IdEmpresa,
                    emp.Nombre,
                    emp.Direccion,
                    emp.Nit,
                    emp.PasswordCantidadMayusculas,
                    emp.PasswordCantidadMinusculas,
                    emp.PasswordCantidadCaracteresEspeciales,
                    emp.PasswordCantidadCaducidadDias,
                    emp.PasswordLargo,
                    emp.PasswordIntentosAntesDeBloquear,
                    emp.PasswordCantidadNumeros,
                    emp.PasswordCantidadPreguntasValidar,
                    FechaCreacion = F(emp.FechaCreacion),
                    FechaModificacion = F(emp.FechaModificacion),
                    emp.UsuarioCreacion,
                    emp.UsuarioModificacion
                };

                return Ok(new { ok = true, data });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error interno: " + ex.Message));
            }
        }

        // ---------- ACTUALIZAR ----------
        // GET /Empresas/Actualizar?IdEmpresa=&Usuario=&Nombre=&Direccion=&Nit=...
        // Campos Nombre/Direccion/Nit y TODAS las políticas son OPCIONALES
        [HttpGet]
        [Route("Actualizar")]
        public async Task<IHttpActionResult> Actualizar(
            int IdEmpresa,
            string Usuario,
            string Nombre = null,
            string Direccion = null,
            string Nit = null,
            int? PasswordCantidadMayusculas = null,
            int? PasswordCantidadMinusculas = null,
            int? PasswordCantidadCaracteresEspeciales = null,
            int? PasswordCantidadCaducidadDias = null,
            int? PasswordLargo = null,
            int? PasswordIntentosAntesDeBloquear = null,
            int? PasswordCantidadNumeros = null,
            int? PasswordCantidadPreguntasValidar = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario))
                    return Ok(new { ok = false, error = "Usuario es requerido." });

                // Permiso: Cambio sobre Empresas
                if (!await SeguridadHelper.TienePermisoAsync(Usuario, Opciones.Empresas, PermisoAccion.Cambio))
                    return Denegado(PermisoAccion.Cambio);

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Empresa_Actualizar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdEmpresa", SqlDbType.Int).Value = IdEmpresa;

                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 100).Value =
                        string.IsNullOrWhiteSpace(Nombre) ? (object)DBNull.Value : Nombre;

                    cmd.Parameters.Add("@Direccion", SqlDbType.VarChar, 200).Value =
                        string.IsNullOrWhiteSpace(Direccion) ? (object)DBNull.Value : Direccion;

                    cmd.Parameters.Add("@Nit", SqlDbType.VarChar, 20).Value =
                        string.IsNullOrWhiteSpace(Nit) ? (object)DBNull.Value : Nit;

                    cmd.Parameters.Add("@PasswordCantidadMayusculas", SqlDbType.Int).Value = (object)PasswordCantidadMayusculas ?? DBNull.Value;
                    cmd.Parameters.Add("@PasswordCantidadMinusculas", SqlDbType.Int).Value = (object)PasswordCantidadMinusculas ?? DBNull.Value;
                    cmd.Parameters.Add("@PasswordCantidadCaracteresEspeciales", SqlDbType.Int).Value = (object)PasswordCantidadCaracteresEspeciales ?? DBNull.Value;
                    cmd.Parameters.Add("@PasswordCantidadCaducidadDias", SqlDbType.Int).Value = (object)PasswordCantidadCaducidadDias ?? DBNull.Value;
                    cmd.Parameters.Add("@PasswordLargo", SqlDbType.Int).Value = (object)PasswordLargo ?? DBNull.Value;
                    cmd.Parameters.Add("@PasswordIntentosAntesDeBloquear", SqlDbType.Int).Value = (object)PasswordIntentosAntesDeBloquear ?? DBNull.Value;
                    cmd.Parameters.Add("@PasswordCantidadNumeros", SqlDbType.Int).Value = (object)PasswordCantidadNumeros ?? DBNull.Value;
                    cmd.Parameters.Add("@PasswordCantidadPreguntasValidar", SqlDbType.Int).Value = (object)PasswordCantidadPreguntasValidar ?? DBNull.Value;

                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = Usuario;

                    cn.Open();
                    cmd.ExecuteNonQuery();
                }

                var emp = ObtenerPorId(IdEmpresa);
                if (emp == null) return Ok(new { ok = false, error = "No encontrado" });

                var data = new
                {
                    emp.IdEmpresa,
                    emp.Nombre,
                    emp.Direccion,
                    emp.Nit,
                    emp.PasswordCantidadMayusculas,
                    emp.PasswordCantidadMinusculas,
                    emp.PasswordCantidadCaracteresEspeciales,
                    emp.PasswordCantidadCaducidadDias,
                    emp.PasswordLargo,
                    emp.PasswordIntentosAntesDeBloquear,
                    emp.PasswordCantidadNumeros,
                    emp.PasswordCantidadPreguntasValidar,
                    FechaCreacion = F(emp.FechaCreacion),
                    FechaModificacion = F(emp.FechaModificacion),
                    emp.UsuarioCreacion,
                    emp.UsuarioModificacion
                };

                return Ok(new { ok = true, data });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error interno: " + ex.Message));
            }
        }

        // ---------- ELIMINAR ----------
        // GET /Empresas/Eliminar?IdEmpresa=&Usuario=
        [HttpGet]
        [Route("Eliminar")]
        public async Task<IHttpActionResult> Eliminar(int IdEmpresa, string Usuario)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario))
                    return Ok(new { ok = false, error = "Usuario es requerido." });

                // Permiso: Baja sobre Empresas
                if (!await SeguridadHelper.TienePermisoAsync(Usuario, Opciones.Empresas, PermisoAccion.Baja))
                    return Denegado(PermisoAccion.Baja);

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Empresa_Eliminar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdEmpresa", SqlDbType.Int).Value = IdEmpresa;

                    cn.Open();
                    cmd.ExecuteNonQuery();
                }

                return Ok(new { ok = true, message = "Eliminado correctamente", id = IdEmpresa });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error interno: " + ex.Message));
            }
        }

        // ---------- Helper interno ----------
        private EmpresaDto ObtenerPorId(int id)
        {
            using (var cn = new SqlConnection(Cnx))
            using (var cmd = new SqlCommand("dbo.sp_Empresa_Listar", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@IdEmpresa", SqlDbType.Int).Value = id;
                cmd.Parameters.Add("@BuscarNombre", SqlDbType.VarChar, 100).Value = DBNull.Value;
                cmd.Parameters.Add("@Nit", SqlDbType.VarChar, 20).Value = DBNull.Value;
                cmd.Parameters.Add("@Page", SqlDbType.Int).Value = 1;
                cmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = 1;

                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read()) return null;

                    return new EmpresaDto
                    {
                        IdEmpresa = Convert.ToInt32(rd["IdEmpresa"]),
                        Nombre = rd["Nombre"] as string,
                        Direccion = rd["Direccion"] as string,
                        Nit = rd["Nit"] as string,
                        PasswordCantidadMayusculas = rd["PasswordCantidadMayusculas"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["PasswordCantidadMayusculas"]),
                        PasswordCantidadMinusculas = rd["PasswordCantidadMinusculas"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["PasswordCantidadMinusculas"]),
                        PasswordCantidadCaracteresEspeciales = rd["PasswordCantidadCaracteresEspeciales"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["PasswordCantidadCaracteresEspeciales"]),
                        PasswordCantidadCaducidadDias = rd["PasswordCantidadCaducidadDias"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["PasswordCantidadCaducidadDias"]),
                        PasswordLargo = rd["PasswordLargo"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["PasswordLargo"]),
                        PasswordIntentosAntesDeBloquear = rd["PasswordIntentosAntesDeBloquear"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["PasswordIntentosAntesDeBloquear"]),
                        PasswordCantidadNumeros = rd["PasswordCantidadNumeros"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["PasswordCantidadNumeros"]),
                        PasswordCantidadPreguntasValidar = rd["PasswordCantidadPreguntasValidar"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["PasswordCantidadPreguntasValidar"]),
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
