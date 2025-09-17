using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Data.Entity.Core.EntityClient;
using System.Web.Mvc;
using ProyectoAnalisis.Models;

namespace ProyectoAnalisis.Controllers
{
    // ===== Request que envías al endpoint =====
    public class LoginRequest
    {
        public string Usuario { get; set; }
        public string Password { get; set; }
        public string Ip { get; set; }
        public string UserAgent { get; set; }
        public string SistemaOperativo { get; set; }
        public string Dispositivo { get; set; }
        public string Browser { get; set; }
        public bool Debug { get; set; } = false;
    }

    // ===== NUEVO DTO, alineado con la salida del SP =====
    public class LoginResult
    {
        public int StatusCode { get; set; }                 // 200, 401, 423, 404, 500
        public string Message { get; set; }                 // Texto del SP
        public bool RequiresPasswordChange { get; set; }    // true => redirigir a cambio de contraseña
        public int? AttemptsRemaining { get; set; }         // intentos restantes (puede ser null)
        public bool IsBlocked { get; set; }                 // true => usuario bloqueado
        public string IdUsuario { get; set; }               // puede venir null en errores
        public string Sesion { get; set; }                  // solo cuando 200
    }

    public class LoginController : Controller
    {
        private static string Clip(string s, int max) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length > max ? s.Substring(0, max) : s);

        private static string GetStringOrNull(IDataRecord r, string col)
        {
            var i = r.GetOrdinal(col);
            return r.IsDBNull(i) ? null : r.GetValue(i).ToString();
        }

        private static bool GetBoolSafe(IDataRecord r, string col)
        {
            var i = r.GetOrdinal(col);
            if (r.IsDBNull(i)) return false;
            var v = r.GetValue(i);
            if (v is bool b) return b;
            return Convert.ToInt32(v) != 0;
        }

        private static int? GetIntNullable(IDataRecord r, string col)
        {
            var i = r.GetOrdinal(col);
            return r.IsDBNull(i) ? (int?)null : Convert.ToInt32(r.GetValue(i));
        }

        private static string GetSqlConnStringFromEF(string efName)
        {
            var ef = ConfigurationManager.ConnectionStrings[efName].ConnectionString;
            var ecb = new EntityConnectionStringBuilder(ef);
            return ecb.ProviderConnectionString;
        }

        // IP del cliente
        private string GetClientIp()
        {
            string[] hdrs = { "CF-Connecting-IP", "X-Forwarded-For", "X-Real-IP", "X-Original-For" };
            foreach (var h in hdrs)
            {
                var v = Request.Headers[h];
                if (!string.IsNullOrWhiteSpace(v))
                {
                    var ip = v.Split(',')[0].Trim();
                    return ip == "::1" ? "127.0.0.1" : ip;
                }
            }
            var addr = Request.UserHostAddress;
            if (string.IsNullOrWhiteSpace(addr))
                addr = Request.ServerVariables["REMOTE_ADDR"];
            return addr == "::1" ? "127.0.0.1" : addr;
        }

        // Info básica del agente
        private (string OS, string Device, string Browser) GetAgentInfo()
        {
            var br = Request.Browser;
            var os = br?.Platform ?? "Desconocido";
            var device = (br?.IsMobileDevice ?? false) ? "Mobile" : "Desktop";
            var browser = br != null ? $"{br.Browser} {br.Version}" : "Desconocido";
            return (os, device, browser);
        }

        // POST /Login/ValidarCredenciales
        [HttpPost]
        [AllowAnonymous]
        public ActionResult ValidarCredenciales(LoginRequest req)
        {
            var resp = new ApiResponse<LoginResult>
            {
                Exito = false,
                Mensaje = "Error inesperado.",
                Datos = null,
                Debug = null
            };

            try
            {
                // Normalizar/Completar metadatos del request
                var ip = string.IsNullOrWhiteSpace(req.Ip) ? GetClientIp() : Clip(req.Ip, 50);
                var userAgent = string.IsNullOrWhiteSpace(req.UserAgent) ? (Request.UserAgent ?? "") : Clip(req.UserAgent, 200);
                var info = GetAgentInfo();
                var sistemaOperativo = string.IsNullOrWhiteSpace(req.SistemaOperativo) ? info.OS : Clip(req.SistemaOperativo, 50);
                var dispositivo = string.IsNullOrWhiteSpace(req.Dispositivo) ? info.Device : Clip(req.Dispositivo, 50);
                var browser = string.IsNullOrWhiteSpace(req.Browser) ? info.Browser : Clip(req.Browser, 50);

                if (req.Debug)
                {
                    resp.Debug = new
                    {
                        Ip = ip,
                        UserAgent = userAgent,
                        SistemaOperativo = sistemaOperativo,
                        Dispositivo = dispositivo,
                        Browser = browser
                    };
                }

                // Cadena de conexión (desde el EDMX)
                var sqlConnStr = GetSqlConnStringFromEF("ProyectoAnalisisEntities1");

                using (var conn = new SqlConnection(sqlConnStr))
                using (var cmd = new SqlCommand("sp_LoginUsuario", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = Clip(req.Usuario, 100);
                    cmd.Parameters.Add("@Password", SqlDbType.VarChar, 200).Value = Clip(req.Password, 200);
                    cmd.Parameters.Add("@DireccionIp", SqlDbType.VarChar, 50).Value = ip;
                    cmd.Parameters.Add("@UserAgent", SqlDbType.VarChar, 200).Value = userAgent;
                    cmd.Parameters.Add("@SistemaOperativo", SqlDbType.VarChar, 50).Value = sistemaOperativo;
                    cmd.Parameters.Add("@Dispositivo", SqlDbType.VarChar, 50).Value = dispositivo;
                    cmd.Parameters.Add("@Browser", SqlDbType.VarChar, 50).Value = browser;

                    conn.Open();

                    using (var reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                    {
                        if (reader.Read())
                        {
                            // El SP **siempre** devuelve estas columnas
                            var statusCode = Convert.ToInt32(reader["StatusCode"]);
                            var message = Convert.ToString(reader["Message"]);

                            var result = new LoginResult
                            {
                                StatusCode = statusCode,
                                Message = message,
                                RequiresPasswordChange = GetBoolSafe(reader, "RequiresPasswordChange"),
                                AttemptsRemaining = GetIntNullable(reader, "AttemptsRemaining"),
                                IsBlocked = GetBoolSafe(reader, "IsBlocked"),
                                IdUsuario = GetStringOrNull(reader, "IdUsuario"),
                                Sesion = GetStringOrNull(reader, "Sesion")
                            };

                            resp.Datos = result;
                            resp.Exito = (statusCode == 200);
                            resp.Mensaje = message;
                        }
                        else
                        {
                            resp.Exito = false;
                            resp.Mensaje = "No se obtuvo respuesta del procedimiento.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                resp.Exito = false;
                resp.Mensaje = "Excepción: " + ex.Message;
            }

            return Json(resp);
        }
    }
}
