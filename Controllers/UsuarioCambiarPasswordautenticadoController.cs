using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using ProyectoAnalisis.Helpers;        // <-- si usas validación de permisos (opcional)
using ProyectoAnalisis.Permissions;    // <-- si usas validación de permisos (opcional)

namespace ProyectoAnalisis.Controllers
{
    // ====== DTO ======
    public class CambiarPasswordRequest
    {
        public string IdUsuario { get; set; }
        public string PasswordActual { get; set; }
        public string PasswordNueva { get; set; }
        public string UsuarioAccion { get; set; }   // quien ejecuta la acción
        public string DireccionIp { get; set; }     // opcional (si no se envía lo inferimos)
        public string UserAgent { get; set; }       // opcional (si no se envía lo inferimos)
    }

    [RoutePrefix("Usuarios")]
    public class UsuariosCambiarPasswordController : ApiController
    {
        // Ajusta tamaños a lo que tengas en BD (si usas NVARCHAR cambia SqlDbType.VarChar por NVarChar)
        private const int PASSWORD_MAX = 100;
        private const int UA_MAX = 200;
        private const int IP_MAX = 50;
        private const int USER_MAX = 100;

        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;

        // --- Helpers sin OWIN ---

        private string GetClientIp()
        {
            try
            {
                // 1) Detrás de proxy/CDN
                if (Request.Headers.TryGetValues("X-Forwarded-For", out var fwd))
                {
                    var ip = fwd.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(ip))
                        return ip.Split(',')[0].Trim();
                }
                if (Request.Headers.TryGetValues("X-Real-IP", out var real))
                {
                    var ip = real.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(ip))
                        return ip.Trim();
                }

                // 2) Directo desde ASP.NET
                var direct = HttpContext.Current?.Request?.UserHostAddress;
                return string.IsNullOrWhiteSpace(direct) ? "127.0.0.1" : direct;
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        private string GetUserAgent()
        {
            var ua = HttpContext.Current?.Request?.UserAgent;
            if (!string.IsNullOrWhiteSpace(ua)) return ua;

            // Fallback a cabeceras de Web API
            return Request?.Headers?.UserAgent?.ToString() ?? "N/A";
        }

        // --- Endpoint ---

        [HttpPost]
        [Route("CambiarPassword")]
        public async Task<IHttpActionResult> CambiarPassword([FromBody] CambiarPasswordRequest req)
        {
            // Validación mínima
            if (req == null ||
                string.IsNullOrWhiteSpace(req.IdUsuario) ||
                string.IsNullOrWhiteSpace(req.PasswordActual) ||
                string.IsNullOrWhiteSpace(req.PasswordNueva))
            {
                return Ok(new { Resultado = 0, Mensaje = "Debe enviar IdUsuario, PasswordActual y PasswordNueva." });
            }

            try
            {
                // Normaliza metadatos
                var usuarioAccion = string.IsNullOrWhiteSpace(req.UsuarioAccion)
                    ? req.IdUsuario.Trim()
                    : req.UsuarioAccion.Trim();

                var ip = string.IsNullOrWhiteSpace(req.DireccionIp) ? GetClientIp() : req.DireccionIp.Trim();
                var userAgent = string.IsNullOrWhiteSpace(req.UserAgent) ? GetUserAgent() : req.UserAgent.Trim();

                // (OPCIONAL) Seguridad:
                // Si alguien distinto cambia la clave de otro usuario, exige permiso de "Cambio" en opción "Usuarios".
                if (!usuarioAccion.Equals(req.IdUsuario.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    var tiene = await SeguridadHelper.TienePermisoAsync(usuarioAccion, Opciones.Usuarios, PermisoAccion.Cambio);
                    if (!tiene) return Ok(new { Resultado = 0, Mensaje = "Permiso denegado (Cambio)." });
                }

                using (var conn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Usuario_CambiarPassword", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Ajusta SqlDbType.* y longitudes si tu SP/tabla usan NVARCHAR u otros tamaños.
                    cmd.Parameters.Add("@IdUsuario", SqlDbType.VarChar, USER_MAX).Value = req.IdUsuario.Trim();
                    cmd.Parameters.Add("@PasswordActual", SqlDbType.VarChar, PASSWORD_MAX).Value = req.PasswordActual;
                    cmd.Parameters.Add("@PasswordNueva", SqlDbType.VarChar, PASSWORD_MAX).Value = req.PasswordNueva;
                    cmd.Parameters.Add("@UsuarioAccion", SqlDbType.VarChar, USER_MAX).Value = usuarioAccion;
                    cmd.Parameters.Add("@DireccionIp", SqlDbType.VarChar, IP_MAX).Value = (object)ip ?? DBNull.Value;
                    cmd.Parameters.Add("@UserAgent", SqlDbType.VarChar, UA_MAX).Value = (object)userAgent ?? DBNull.Value;

                    await conn.OpenAsync();
                    using (var rd = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow))
                    {
                        if (!rd.Read())
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "";

                        return Ok(new { Resultado = resultado, Mensaje = mensaje });
                    }
                }
            }
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error interno: " + e.Message));
            }
        }
    }
}
