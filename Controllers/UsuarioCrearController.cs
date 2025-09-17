using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Data.Entity.Core.EntityClient;
using System.Globalization;
using System.Web.Http;

namespace ProyectoAnalisis.Controllers
{
    // ===== DTO =====
    public class UsuarioCrearRequest
    {
        public string IdUsuario { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }

        // Aceptamos "yyyy-MM-dd" o "yyyy-MM-ddTHH:mm:ss"
        public string FechaNacimiento { get; set; }

        public int? IdGenero { get; set; }
        public string CorreoElectronico { get; set; }
        public string TelefonoMovil { get; set; }
        public int? IdSucursal { get; set; }
        public string Pregunta { get; set; }
        public string Respuesta { get; set; }
        public int? IdRole { get; set; }
        public string Password { get; set; }
        public int? IdStatusUsuario { get; set; } = 1;
        public string UsuarioAccion { get; set; } = "system";

        // Foto opcional en base64 (puede venir como data URL o como base64 puro)
        public string FotografiaBase64 { get; set; }
    }

    [RoutePrefix("Usuarios")]
    public class UsuariosCrearController : ApiController
    {
        // --- Connection string robusta: usa ConexionBD; si no existe, intenta Entities/ProyectoAnalisisEntities1 ---
        private static string Cnx
        {
            get
            {
                var cs = ConfigurationManager.ConnectionStrings["ConexionBD"];
                if (cs != null && !string.IsNullOrWhiteSpace(cs.ConnectionString))
                    return cs.ConnectionString;

                var ef = ConfigurationManager.ConnectionStrings["Entities"]
                         ?? ConfigurationManager.ConnectionStrings["ProyectoAnalisisEntities1"];
                if (ef != null)
                {
                    var ecb = new EntityConnectionStringBuilder(ef.ConnectionString);
                    return ecb.ProviderConnectionString;
                }

                throw new InvalidOperationException(
                    "No se encontró la cadena 'ConexionBD' ni una cadena EF ('Entities' o 'ProyectoAnalisisEntities1') en Web.config.");
            }
        }

        [HttpPost]
        [Route("Crear")]
        public IHttpActionResult Crear([FromBody] UsuarioCrearRequest req)
        {
            try
            {
                if (req == null)
                    return Ok(new { Resultado = 0, Mensaje = "Body requerido." });

                // --- Validaciones mínimas de presencia ---
                if (string.IsNullOrWhiteSpace(req.IdUsuario) ||
                    string.IsNullOrWhiteSpace(req.Nombre) ||
                    string.IsNullOrWhiteSpace(req.Apellido) ||
                    string.IsNullOrWhiteSpace(req.FechaNacimiento) ||
                    req.IdGenero == null ||
                    req.IdSucursal == null ||
                    string.IsNullOrWhiteSpace(req.Pregunta) ||
                    string.IsNullOrWhiteSpace(req.Respuesta) ||
                    req.IdRole == null ||
                    string.IsNullOrWhiteSpace(req.Password))
                {
                    return Ok(new { Resultado = 0, Mensaje = "Campos requeridos faltantes." });
                }

                // --- Parseo de FechaNacimiento ---
                var formatos = new[] { "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss" };
                if (!DateTime.TryParseExact(req.FechaNacimiento, formatos, CultureInfo.InvariantCulture,
                                            DateTimeStyles.None, out DateTime fn))
                {
                    return Ok(new { Resultado = 0, Mensaje = "FechaNacimiento inválida. Use yyyy-MM-dd o yyyy-MM-ddTHH:mm:ss." });
                }

                // --- Decodificar FotografiaBase64 (opcional) ---
                byte[] fotoBytes = null;
                if (!string.IsNullOrWhiteSpace(req.FotografiaBase64))
                {
                    try
                    {
                        var b64 = req.FotografiaBase64.Trim();
                        var comma = b64.IndexOf(',');
                        if (comma > -1) b64 = b64.Substring(comma + 1); // soporta data URL
                        fotoBytes = Convert.FromBase64String(b64);
                    }
                    catch
                    {
                        return Ok(new { Resultado = 0, Mensaje = "Fotografía inválida (base64)." });
                    }
                }

                // --- Normalizar opcionales vacíos a NULL ---
                var correo = string.IsNullOrWhiteSpace(req.CorreoElectronico) ? null : req.CorreoElectronico.Trim();
                var telefono = string.IsNullOrWhiteSpace(req.TelefonoMovil) ? null : req.TelefonoMovil.Trim();
                var usuarioAcc = string.IsNullOrWhiteSpace(req.UsuarioAccion) ? "system" : req.UsuarioAccion.Trim();


                using (var conn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Usuario_Crear", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Requeridos
                    cmd.Parameters.Add("@IdUsuario", SqlDbType.VarChar, 100).Value = req.IdUsuario.Trim();
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 100).Value = req.Nombre.Trim();
                    cmd.Parameters.Add("@Apellido", SqlDbType.VarChar, 100).Value = req.Apellido.Trim();
                    cmd.Parameters.Add("@FechaNacimiento", SqlDbType.Date).Value = fn.Date;
                    cmd.Parameters.Add("@IdGenero", SqlDbType.Int).Value = req.IdGenero;
                    cmd.Parameters.Add("@IdSucursal", SqlDbType.Int).Value = req.IdSucursal;
                    cmd.Parameters.Add("@Pregunta", SqlDbType.VarChar, 200).Value = req.Pregunta.Trim();
                    cmd.Parameters.Add("@Respuesta", SqlDbType.VarChar, 200).Value = req.Respuesta.Trim();
                    cmd.Parameters.Add("@IdRole", SqlDbType.Int).Value = req.IdRole;
                    cmd.Parameters.Add("@Password", SqlDbType.NVarChar, 200).Value = req.Password;

                    // Opcionales
                    cmd.Parameters.Add("@CorreoElectronico", SqlDbType.VarChar, 100).Value =
                        (object)correo ?? DBNull.Value;
                    cmd.Parameters.Add("@TelefonoMovil", SqlDbType.VarChar, 30).Value =
                        (object)telefono ?? DBNull.Value;
                    cmd.Parameters.Add("@IdStatusUsuario", SqlDbType.Int).Value =
                        (object)req.IdStatusUsuario ?? DBNull.Value;
                    cmd.Parameters.Add("@UsuarioAccion", SqlDbType.VarChar, 100).Value =
                        (object)usuarioAcc ?? DBNull.Value;

                    // Foto (opcional)
                    cmd.Parameters.Add("@Fotografia", SqlDbType.VarBinary, -1).Value =
                        (object)fotoBytes ?? DBNull.Value;

                    conn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.HasRows)
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        // RS #1: Resultado / Mensaje
                        rd.Read();
                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "";

                        if (resultado != 1)
                            return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        // RS #2: datos del usuario creado
                        object data = null;
                        if (rd.NextResult() && rd.Read())
                        {
                            data = new
                            {
                                IdUsuario = rd["IdUsuario"] as string,
                                Nombre = rd["Nombre"] as string,
                                Apellido = rd["Apellido"] as string,
                                CorreoElectronico = rd["CorreoElectronico"] as string,
                                IdSucursal = rd["IdSucursal"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["IdSucursal"]),
                                IdStatusUsuario = rd["IdStatusUsuario"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["IdStatusUsuario"]),
                                IdRole = rd["IdRole"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["IdRole"])
                            };
                        }

                        return Ok(new { Resultado = resultado, Mensaje = mensaje, Data = data });
                    }
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error interno: " + ex.Message));
            }
        }
    }
}
