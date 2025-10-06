using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Web.Http;
using ProyectoAnalisis.Helpers;
using ProyectoAnalisis.Permissions;

namespace ProyectoAnalisis.Controllers
{
    [RoutePrefix("Usuarios")]
    public class UsuariosObtenerController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;
        private static string Fmt(DateTime? dt) => dt?.ToString("yyyy-MM-ddTHH:mm:ss");

        [HttpGet]
        [Route("Obtener")]
        public async Task<IHttpActionResult> Obtener(
            string idUsuario = null,
            string correoElectronico = null,
            bool incluirFoto = false,
            bool incluirAuditoria = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idUsuario) && string.IsNullOrWhiteSpace(correoElectronico))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar idUsuario o correoElectronico." });

                using (var conn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Usuario_Obtener", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdUsuario", SqlDbType.VarChar, 100).Value =
                        (object)(string.IsNullOrWhiteSpace(idUsuario) ? null : idUsuario.Trim()) ?? DBNull.Value;
                    cmd.Parameters.Add("@CorreoElectronico", SqlDbType.VarChar, 100).Value =
                        (object)(string.IsNullOrWhiteSpace(correoElectronico) ? null : correoElectronico.Trim()) ?? DBNull.Value;
                    cmd.Parameters.Add("@IncluirFoto", SqlDbType.Bit).Value = incluirFoto;
                    cmd.Parameters.Add("@IncluirAuditoria", SqlDbType.Bit).Value = incluirAuditoria;

                    await conn.OpenAsync();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        if (!await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int resultado = rd["Resultado"] != DBNull.Value ? Convert.ToInt32(rd["Resultado"]) : 0;
                        string mensaje = rd["Mensaje"] as string ?? "";

                        if (resultado != 1)
                            return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        if (!await rd.NextResultAsync() || !await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "No se encontraron datos del usuario." });

                        Func<string, string> S = n => rd[n] == DBNull.Value ? null : (string)rd[n];
                        Func<string, int?> I = n => rd[n] == DBNull.Value ? (int?)null : Convert.ToInt32(rd[n]);
                        Func<string, DateTime?> D = n => rd[n] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd[n]);

                        string fotoBase64 = null;
                        if (incluirFoto && rd["Fotografia"] != DBNull.Value)
                            fotoBase64 = Convert.ToBase64String((byte[])rd["Fotografia"]);

                        var data = new
                        {
                            IdUsuario = S("IdUsuario"),
                            Nombre = S("Nombre"),
                            Apellido = S("Apellido"),
                            FechaNacimiento = Fmt(D("FechaNacimiento")),
                            IdStatusUsuario = I("IdStatusUsuario"),
                            IdGenero = I("IdGenero"),
                            CorreoElectronico = S("CorreoElectronico"),
                            TelefonoMovil = S("TelefonoMovil"),
                            IdSucursal = I("IdSucursal"),
                            Pregunta = S("Pregunta"),
                            IdRole = I("IdRole"),
                            UltimaFechaIngreso = Fmt(D("UltimaFechaIngreso")),
                            IntentosDeAcceso = I("IntentosDeAcceso"),
                            SesionActual = S("SesionActual"),
                            UltimaFechaCambioPassword = Fmt(D("UltimaFechaCambioPassword")),
                            FotografiaBase64 = fotoBase64,
                            FechaCreacion = Fmt(D("FechaCreacion")),
                            UsuarioCreacion = S("UsuarioCreacion"),
                            FechaModificacion = Fmt(D("FechaModificacion")),
                            UsuarioModificacion = S("UsuarioModificacion")
                        };

                        return Ok(new { Resultado = 1, Mensaje = "OK", Data = data });
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
