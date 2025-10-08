using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Data.Entity.Core.EntityClient;
using System.Globalization;
using System.Threading.Tasks;
using System.Web.Http;
using ProyectoAnalisis.Helpers;
using ProyectoAnalisis.Permissions;


namespace ProyectoAnalis.Controllers
{
    // ===== DTO =====
    public class UsuarioActualizarRequest
    {
        public string IdUsuario { get; set; }           // requerido
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public string FechaNacimiento { get; set; }     // "yyyy-MM-dd" o "yyyy-MM-ddTHH:mm:ss"
        public int? IdStatusUsuario { get; set; }
        public string Password { get; set; }
        public int? IdGenero { get; set; }
        public string CorreoElectronico { get; set; }
        public string TelefonoMovil { get; set; }
        public int? IdSucursal { get; set; }
        public string Pregunta { get; set; }
        public string Respuesta { get; set; }
        public int? IdRole { get; set; }
        public string FotografiaBase64 { get; set; }    // puede traer "data:image/...;base64,"
        public bool LimpiarFoto { get; set; } = false;
        public string UsuarioAccion { get; set; }       // requerido para auditar/validar permiso
    }

    [RoutePrefix("Usuarios")]
    public class UsuariosActualizarController : ApiController
    {
        // Conexión robusta
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
                    "No se encontró la cadena 'ConexionBD' ni una EF ('Entities' o 'ProyectoAnalisisEntities1').");
            }
        }

        // Helper: decodifica Base64 con o sin encabezado data:
        private static byte[] FromBase64(string b64)
        {
            if (string.IsNullOrWhiteSpace(b64)) return null;
            var s = b64.Trim();
            var comma = s.IndexOf(',');
            if (comma >= 0) s = s.Substring(comma + 1);
            return Convert.FromBase64String(s);
        }

        private IHttpActionResult Denegado(PermisoAccion acc)
            => Ok(new { Resultado = 0, Mensaje = $"Permiso denegado ({acc})." });

        [HttpPost]
        [Route("Actualizar")]
        public async Task<IHttpActionResult> ActualizarPost([FromBody] UsuarioActualizarRequest req)
        {
            try
            {
                if (req == null || string.IsNullOrWhiteSpace(req.IdUsuario))
                    return Ok(new { Resultado = 0, Mensaje = "Body inválido o falta IdUsuario." });

                // UsuarioAccion requerido para validar permiso
                var usuarioAccion = string.IsNullOrWhiteSpace(req.UsuarioAccion) ? "system" : req.UsuarioAccion.Trim();

                // Validar permiso: CAMBIO sobre la opción "Usuarios"
                if (!await SeguridadHelper.TienePermisoAsync(usuarioAccion, Opciones.Usuarios, PermisoAccion.Cambio))
                    return Denegado(PermisoAccion.Cambio);

                // Fecha (opcional)
                DateTime? fechaNac = null;
                if (!string.IsNullOrWhiteSpace(req.FechaNacimiento))
                {
                    var formatos = new[] { "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss" };
                    if (!DateTime.TryParseExact(req.FechaNacimiento, formatos, CultureInfo.InvariantCulture,
                                                DateTimeStyles.None, out var fn))
                        return Ok(new { Resultado = 0, Mensaje = "FechaNacimiento inválida. Use yyyy-MM-dd o yyyy-MM-ddTHH:mm:ss." });
                    fechaNac = fn.Date;
                }

                // Foto (opcional)
                byte[] fotoBytes = null;
                if (!req.LimpiarFoto && !string.IsNullOrWhiteSpace(req.FotografiaBase64))
                {
                    try { fotoBytes = FromBase64(req.FotografiaBase64); }
                    catch { return Ok(new { Resultado = 0, Mensaje = "FotografiaBase64 inválida (no es Base64)." }); }
                }

                using (var conn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Usuario_Actualizar", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Requerido
                    cmd.Parameters.Add("@IdUsuario", SqlDbType.VarChar, 100).Value = req.IdUsuario.Trim();

                    // Opcionales (el SP solo actualiza si no viene NULL)
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 100).Value = (object)req.Nombre ?? DBNull.Value;
                    cmd.Parameters.Add("@Apellido", SqlDbType.VarChar, 100).Value = (object)req.Apellido ?? DBNull.Value;
                    cmd.Parameters.Add("@FechaNacimiento", SqlDbType.Date).Value = (object)fechaNac ?? DBNull.Value;
                    cmd.Parameters.Add("@IdStatusUsuario", SqlDbType.Int).Value = (object)req.IdStatusUsuario ?? DBNull.Value;
                    cmd.Parameters.Add("@Password", SqlDbType.VarChar, 100).Value = (object)req.Password ?? DBNull.Value; // VarChar
                    cmd.Parameters.Add("@IdGenero", SqlDbType.Int).Value = (object)req.IdGenero ?? DBNull.Value;
                    cmd.Parameters.Add("@CorreoElectronico", SqlDbType.VarChar, 100).Value = (object)req.CorreoElectronico ?? DBNull.Value;
                    cmd.Parameters.Add("@TelefonoMovil", SqlDbType.VarChar, 30).Value = (object)req.TelefonoMovil ?? DBNull.Value;
                    cmd.Parameters.Add("@IdSucursal", SqlDbType.Int).Value = (object)req.IdSucursal ?? DBNull.Value;
                    cmd.Parameters.Add("@Pregunta", SqlDbType.VarChar, 200).Value = (object)req.Pregunta ?? DBNull.Value;
                    cmd.Parameters.Add("@Respuesta", SqlDbType.VarChar, 200).Value = (object)req.Respuesta ?? DBNull.Value;
                    cmd.Parameters.Add("@IdRole", SqlDbType.Int).Value = (object)req.IdRole ?? DBNull.Value;

                    // Foto / limpiar
                    var pFoto = cmd.Parameters.Add("@Fotografia", SqlDbType.VarBinary, -1);
                    pFoto.Value = (object)fotoBytes ?? DBNull.Value;
                    cmd.Parameters.Add("@LimpiarFoto", SqlDbType.Bit).Value = req.LimpiarFoto;

                    // Auditoría
                    cmd.Parameters.Add("@UsuarioAccion", SqlDbType.VarChar, 100).Value = usuarioAccion;

                    conn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.HasRows)
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        // RS#1: Resultado/Mensaje
                        rd.Read();
                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "";

                        if (resultado != 1)
                            return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        // RS#2: datos del usuario
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
