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
    [RoutePrefix("Roles")]
    public class RolesController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;

        private static string Fmt(object dt) =>
            (dt == DBNull.Value || dt == null) ? null : ((DateTime)dt).ToString("yyyy-MM-ddTHH:mm:ss");

        private IHttpActionResult Denegado(PermisoAccion acc) =>
            Ok(new { Resultado = 0, Mensaje = $"Permiso denegado ({acc})." });

        private IHttpActionResult Denegado(string detalle) =>
            Ok(new { Resultado = 0, Mensaje = $"Permiso denegado ({detalle})." });

        // ========= LISTAR =========
        // GET /Roles/Listar?usuarioAccion=&IdRole=&Nombre=
        // - Sin IdRole/Nombre: devuelve TODOS (según el SP)
        // - Con Nombre: devuelve TOP 1 del nombre exacto (según el SP)
        // - Con IdRole: se envía al SP (si tu SP lo ignora, simplemente no filtrará)
        [HttpGet]
        [Route("Listar")]
        public async Task<IHttpActionResult> Listar(
            string usuarioAccion,
            int? IdRole = null,
            string Nombre = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(usuarioAccion))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar usuarioAccion." });

                var u = usuarioAccion.Trim();
                var puede =
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Roles, PermisoAccion.Imprimir) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Roles, PermisoAccion.Exportar) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Roles, PermisoAccion.Cambio) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Roles, PermisoAccion.Alta) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Roles, PermisoAccion.Baja);

                if (!puede) return Denegado("lectura");

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Role_Listar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdRole", SqlDbType.Int).Value = (object)IdRole ?? DBNull.Value;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 50).Value =
                        (object)(string.IsNullOrWhiteSpace(Nombre) ? null : Nombre.Trim()) ?? DBNull.Value;

                    cn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        // RS#1: meta
                        if (!await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "OK";
                        if (resultado != 1) return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        // RS#2: datos
                        var items = new List<object>();
                        if (await rd.NextResultAsync())
                        {
                            while (await rd.ReadAsync())
                            {
                                items.Add(new
                                {
                                    IdRole = Convert.ToInt32(rd["IdRole"]),
                                    Nombre = rd["Nombre"] as string,
                                    FechaCreacion = Fmt(rd["FechaCreacion"]),
                                    UsuarioCreacion = rd["UsuarioCreacion"] as string,
                                    FechaModificacion = Fmt(rd["FechaModificacion"]),
                                    UsuarioModificacion = rd["UsuarioModificacion"] as string
                                });
                            }
                        }

                        return Ok(new
                        {
                            Resultado = 1,
                            Mensaje = "OK",
                            Items = items
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error interno: " + ex.Message));
            }
        }

        // ========= CREAR =========
        // GET /Roles/Crear?Usuario=&Nombre=
        [HttpGet]
        [Route("Crear")]
        public async Task<IHttpActionResult> Crear(string Usuario, string Nombre)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario) || string.IsNullOrWhiteSpace(Nombre))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar Usuario y Nombre." });

                var u = Usuario.Trim();
                if (!await SeguridadHelper.TienePermisoAsync(u, Opciones.Roles, PermisoAccion.Alta))
                    return Denegado(PermisoAccion.Alta);

                int nuevoId;
                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Role_Crear", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 50).Value = Nombre.Trim();
                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = u;

                    cn.Open();
                    var scalar = await cmd.ExecuteScalarAsync();
                    nuevoId = Convert.ToInt32(scalar);
                }

                var creado = await ObtenerPorIdAsync(nuevoId);
                return Ok(new { Resultado = 1, Mensaje = "Creado", Data = creado });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error interno: " + ex.Message));
            }
        }

        // ========= ACTUALIZAR =========
        // GET /Roles/Actualizar?Usuario=&IdRole=&Nombre=
        [HttpGet]
        [Route("Actualizar")]
        public async Task<IHttpActionResult> Actualizar(string Usuario, int IdRole, string Nombre = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar Usuario." });

                var u = Usuario.Trim();
                if (!await SeguridadHelper.TienePermisoAsync(u, Opciones.Roles, PermisoAccion.Cambio))
                    return Denegado(PermisoAccion.Cambio);

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Role_Actualizar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdRole", SqlDbType.Int).Value = IdRole;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 50).Value =
                        (object)(string.IsNullOrWhiteSpace(Nombre) ? null : Nombre.Trim()) ?? DBNull.Value;
                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = u;

                    cn.Open();
                    await cmd.ExecuteNonQueryAsync();
                }

                var actualizado = await ObtenerPorIdAsync(IdRole);
                return Ok(new { Resultado = 1, Mensaje = "Actualizado", Data = actualizado });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error interno: " + ex.Message));
            }
        }

        // ========= ELIMINAR =========
        // GET /Roles/Eliminar?Usuario=&IdRole=
        [HttpGet]
        [Route("Eliminar")]
        public async Task<IHttpActionResult> Eliminar(string Usuario, int IdRole)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar Usuario." });

                var u = Usuario.Trim();
                if (!await SeguridadHelper.TienePermisoAsync(u, Opciones.Roles, PermisoAccion.Baja))
                    return Denegado(PermisoAccion.Baja);

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Role_Eliminar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdRole", SqlDbType.Int).Value = IdRole;

                    cn.Open();
                    await cmd.ExecuteNonQueryAsync();
                }

                return Ok(new { Resultado = 1, Mensaje = "Eliminado" });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error interno: " + ex.Message));
            }
        }

        // ===== Helper: re-lectura por Id usando el SP (RS#1 + RS#2) =====
        private async Task<object> ObtenerPorIdAsync(int idRole)
        {
            using (var cn = new SqlConnection(Cnx))
            using (var cmd = new SqlCommand("dbo.sp_Role_Listar", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@IdRole", SqlDbType.Int).Value = idRole;
                cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 50).Value = DBNull.Value;

                cn.Open();
                using (var rd = await cmd.ExecuteReaderAsync())
                {
                    // RS#1 meta
                    if (!await rd.ReadAsync()) return null;
                    int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                    if (resultado != 1) return null;

                    // RS#2 datos (puede ser 0..n; para IdRole esperamos 1)
                    if (!await rd.NextResultAsync() || !await rd.ReadAsync()) return null;

                    return new
                    {
                        IdRole = Convert.ToInt32(rd["IdRole"]),
                        Nombre = rd["Nombre"] as string,
                        FechaCreacion = Fmt(rd["FechaCreacion"]),
                        UsuarioCreacion = rd["UsuarioCreacion"] as string,
                        FechaModificacion = Fmt(rd["FechaModificacion"]),
                        UsuarioModificacion = rd["UsuarioModificacion"] as string
                    };
                }
            }
        }
    }
}
