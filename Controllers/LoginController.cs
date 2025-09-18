using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Data.Entity.Core.EntityClient;
using System.Web.Mvc;
using System.Collections.Generic;
using ProyectoAnalisis.Models;

namespace ProyectoAnalisis.Controllers
{
    // ===== Request =====
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

    // ===== DTOs adicionales =====
    public class PermisoOpcionDto
    {
        public int IdModulo { get; set; }
        public string Modulo { get; set; }
        public int IdMenu { get; set; }
        public string Menu { get; set; }
        public int IdOpcion { get; set; }
        public string Opcion { get; set; }
        public string Pagina { get; set; }
        public int Alta { get; set; }
        public int Baja { get; set; }
        public int Cambio { get; set; }
        public int Imprimir { get; set; }
        public int Exportar { get; set; }
    }

    public class LoginResult
    {
        public int StatusCode { get; set; }                 // 200, 401, 423, 404, 500
        public string Message { get; set; }
        public bool RequiresPasswordChange { get; set; }
        public int? AttemptsRemaining { get; set; }
        public bool IsBlocked { get; set; }
        public string IdUsuario { get; set; }
        public string Sesion { get; set; }
        // Extras para pintar UI
        public List<PermisoOpcionDto> Permisos { get; set; }
        public string NavegacionJson { get; set; }
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

        // ====== HELPERS PARA CARGAR PERMISOS/NAVEGACIÓN ======
        private List<PermisoOpcionDto> CargarPermisos(SqlConnection conn, string idUsuario)
        {
            var permisos = new List<PermisoOpcionDto>();
            using (var cmd = new SqlCommand("dbo.sp_Seguridad_PermisosUsuario", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@IdUsuario", SqlDbType.VarChar, 100).Value = idUsuario;

                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        permisos.Add(new PermisoOpcionDto
                        {
                            IdModulo = Convert.ToInt32(rd["IdModulo"]),
                            Modulo = Convert.ToString(rd["Modulo"]),
                            IdMenu = Convert.ToInt32(rd["IdMenu"]),
                            Menu = Convert.ToString(rd["Menu"]),
                            IdOpcion = Convert.ToInt32(rd["IdOpcion"]),
                            Opcion = Convert.ToString(rd["Opcion"]),
                            Pagina = Convert.ToString(rd["Pagina"]),
                            Alta = Convert.ToInt32(rd["Alta"]),
                            Baja = Convert.ToInt32(rd["Baja"]),
                            Cambio = Convert.ToInt32(rd["Cambio"]),
                            Imprimir = Convert.ToInt32(rd["Imprimir"]),
                            Exportar = Convert.ToInt32(rd["Exportar"])
                        });
                    }
                }
            }
            return permisos;
        }

        private string CargarNavegacion(SqlConnection conn, string idUsuario)
        {
            using (var cmd = new SqlCommand("dbo.sp_Seguridad_Navegacion", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@IdUsuario", SqlDbType.VarChar, 100).Value = idUsuario;

                var json = cmd.ExecuteScalar();
                return json == null || json == DBNull.Value ? "[]" : Convert.ToString(json);
            }
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

                    LoginResult result = null;

                    using (var reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                    {
                        if (reader.Read())
                        {
                            var statusCode = Convert.ToInt32(reader["StatusCode"]);
                            var message = Convert.ToString(reader["Message"]);

                            result = new LoginResult
                            {
                                StatusCode = statusCode,
                                Message = message,
                                RequiresPasswordChange = GetBoolSafe(reader, "RequiresPasswordChange"),
                                AttemptsRemaining = GetIntNullable(reader, "AttemptsRemaining"),
                                IsBlocked = GetBoolSafe(reader, "IsBlocked"),
                                IdUsuario = GetStringOrNull(reader, "IdUsuario"),
                                Sesion = GetStringOrNull(reader, "Sesion"),
                                Permisos = null,
                                NavegacionJson = null
                            };

                            // Para que el JSON coincida con tu "Caso OK"
                            resp.Exito = (statusCode == 200);
                            resp.Mensaje = message;
                            resp.Datos = result;
                        }
                        else
                        {
                            resp.Exito = false;
                            resp.Mensaje = "No se obtuvo respuesta del procedimiento.";
                            return Json(resp);
                        }
                    }

                    // Si login OK, anexar permisos + navegación en la MISMA conexión
                    if (result != null && result.StatusCode == 200 && !string.IsNullOrWhiteSpace(result.IdUsuario))
                    {
                        // 1) Permisos
                        result.Permisos = CargarPermisos(conn, result.IdUsuario);

                        // 2) Navegación (árbol JSON)
                        result.NavegacionJson = CargarNavegacion(conn, result.IdUsuario);
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
