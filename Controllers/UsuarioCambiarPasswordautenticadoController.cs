using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.Http;

namespace ProyectoAnalisis.Controllers
{
    public class CambiarPasswordRequest
    {
        public string IdUsuario { get; set; }
        public string PasswordActual { get; set; }
        public string PasswordNueva { get; set; }
        public string UsuarioAccion { get; set; }
        public string DireccionIp { get; set; }
        public string UserAgent { get; set; }
    }

    [RoutePrefix("Usuarios")]
    public class UsuariosCambiarPasswordController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;

        [HttpPost]
        [Route("CambiarPassword")]
        public IHttpActionResult CambiarPassword([FromBody] CambiarPasswordRequest req)
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
                using (var conn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Usuario_CambiarPassword", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("@IdUsuario", SqlDbType.VarChar, 100).Value = req.IdUsuario.Trim();
                    cmd.Parameters.Add("@PasswordActual", SqlDbType.NVarChar, 200).Value = req.PasswordActual;
                    cmd.Parameters.Add("@PasswordNueva", SqlDbType.NVarChar, 200).Value = req.PasswordNueva;
                    cmd.Parameters.Add("@UsuarioAccion", SqlDbType.VarChar, 100).Value =
                        (object)(req.UsuarioAccion ?? req.IdUsuario) ?? DBNull.Value;
                    cmd.Parameters.Add("@DireccionIp", SqlDbType.VarChar, 50).Value =
                        (object)req.DireccionIp ?? DBNull.Value;
                    cmd.Parameters.Add("@UserAgent", SqlDbType.VarChar, 200).Value =
                        (object)req.UserAgent ?? DBNull.Value;

                    conn.Open();
                    using (var rd = cmd.ExecuteReader())
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
