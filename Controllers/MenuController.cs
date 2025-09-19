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
    [RoutePrefix("Menus")]
    public class MenusController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;

        private static string Fmt(object dt)
            => (dt == DBNull.Value || dt == null) ? null : ((DateTime)dt).ToString("yyyy-MM-ddTHH:mm:ss");

        private IHttpActionResult Denegado(PermisoAccion acc) => Ok(new { Resultado = 0, Mensaje = $"Permiso denegado ({acc})." });
        private IHttpActionResult Denegado(string detalle) => Ok(new { Resultado = 0, Mensaje = $"Permiso denegado ({detalle})." });

        private static readonly HashSet<string> CamposOrden =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "IdMenu", "IdModulo", "Nombre", "OrdenMenu", "FechaCreacion" };

        private static string NormalizarOrdenPor(string v) => CamposOrden.Contains(v ?? "") ? v : "Nombre";
        private static string NormalizarDir(string d) => string.Equals(d, "ASC", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

        // ===== LISTAR (un registro por filtro) =====
        // GET /Menus/Listar?usuarioAccion=&IdMenu=&IdModulo=&Nombre=&Pagina=&incluirAuditoria=false
        [HttpGet]
        [Route("Listar")]
        public async Task<IHttpActionResult> Listar(
            string usuarioAccion,
            int? IdMenu = null,
            int? IdModulo = null,
            string Nombre = null,
            bool incluirAuditoria = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(usuarioAccion))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar usuarioAccion." });

                if (IdMenu == null && IdModulo == null && string.IsNullOrWhiteSpace(Nombre))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar al menos un filtro (IdMenu, IdModulo o Nombre)." });

                var u = usuarioAccion.Trim();
                var puede =
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Menus, PermisoAccion.Imprimir) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Menus, PermisoAccion.Exportar) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Menus, PermisoAccion.Cambio) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Menus, PermisoAccion.Alta) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Menus, PermisoAccion.Baja);
                if (!puede) return Denegado("lectura");

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Menu_Listar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdMenu", SqlDbType.Int).Value = (object)IdMenu ?? DBNull.Value;
                    cmd.Parameters.Add("@IdModulo", SqlDbType.Int).Value = (object)IdModulo ?? DBNull.Value;
                    cmd.Parameters.Add("@BuscarNombre", SqlDbType.VarChar, 100).Value =
                        (object)(string.IsNullOrWhiteSpace(Nombre) ? null : Nombre.Trim()) ?? DBNull.Value;
                    cmd.Parameters.Add("@Page", SqlDbType.Int).Value = 1;
                    cmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = 1;
                    cmd.Parameters.Add("@OrdenPor", SqlDbType.VarChar, 20).Value = "Nombre";
                    cmd.Parameters.Add("@OrdenDir", SqlDbType.VarChar, 4).Value = "ASC";

                    cn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        // RS#1: meta
                        if (!await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "OK";
                        if (resultado != 1) return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        // RS#2: rows (trae 0..1)
                        if (!await rd.NextResultAsync() || !await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "No se encontraron datos." });

                        var data = new
                        {
                            IdMenu = Convert.ToInt32(rd["IdMenu"]),
                            IdModulo = rd["IdModulo"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["IdModulo"]),
                            Nombre = rd["Nombre"] as string,
                            OrdenMenu = rd["OrdenMenu"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["OrdenMenu"]),
                            FechaCreacion = Fmt(rd["FechaCreacion"]),
                            UsuarioCreacion = incluirAuditoria ? rd["UsuarioCreacion"] as string : null,
                            FechaModificacion = incluirAuditoria ? Fmt(rd["FechaModificacion"]) : null,
                            UsuarioModificacion = incluirAuditoria ? rd["UsuarioModificacion"] as string : null
                        };

                        return Ok(new { Resultado = 1, Mensaje = "OK", Data = data });
                    }
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error interno: " + ex.Message));
            }
        }

        // ===== LISTAR BUSQUEDA / Paginación =====
        // GET /Menus/ListarBusqueda?usuarioAccion=&Buscar=&IdModulo=&Pagina=1&TamanoPagina=50&OrdenPor=Nombre&OrdenDir=ASC
        [HttpGet]
        [Route("ListarBusqueda")]
        public async Task<IHttpActionResult> ListarBusqueda(
            string usuarioAccion,
            string Buscar = null,
            int? IdModulo = null,
            int Pagina = 1,
            int TamanoPagina = 50,
            string OrdenPor = "Nombre",
            string OrdenDir = "ASC")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(usuarioAccion))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar usuarioAccion." });

                var u = usuarioAccion.Trim();
                var puede =
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Menus, PermisoAccion.Imprimir) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Menus, PermisoAccion.Exportar) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Menus, PermisoAccion.Cambio) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Menus, PermisoAccion.Alta) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Menus, PermisoAccion.Baja);
                if (!puede) return Denegado("lectura");

                OrdenPor = NormalizarOrdenPor(OrdenPor);
                OrdenDir = NormalizarDir(OrdenDir);

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Menu_Listar_Busqueda", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@Buscar", SqlDbType.VarChar, 100).Value =
                        (object)(string.IsNullOrWhiteSpace(Buscar) ? null : Buscar.Trim()) ?? DBNull.Value;
                    cmd.Parameters.Add("@IdModulo", SqlDbType.Int).Value = (object)IdModulo ?? DBNull.Value;
                    cmd.Parameters.Add("@Page", SqlDbType.Int).Value = Pagina;
                    cmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = TamanoPagina;
                    cmd.Parameters.Add("@OrdenPor", SqlDbType.VarChar, 20).Value = OrdenPor;
                    cmd.Parameters.Add("@OrdenDir", SqlDbType.VarChar, 4).Value = OrdenDir;

                    cn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        // RS#1: meta
                        if (!await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "OK";
                        if (resultado != 1) return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        // RS#2: items
                        if (!await rd.NextResultAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin datos." });

                        var items = new List<object>();
                        while (await rd.ReadAsync())
                        {
                            items.Add(new
                            {
                                IdMenu = Convert.ToInt32(rd["IdMenu"]),
                                IdModulo = rd["IdModulo"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["IdModulo"]),
                                Nombre = rd["Nombre"] as string,
                                OrdenMenu = rd["OrdenMenu"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["OrdenMenu"]),
                                FechaCreacion = Fmt(rd["FechaCreacion"]),
                                UsuarioCreacion = rd["UsuarioCreacion"] as string,
                                FechaModificacion = Fmt(rd["FechaModificacion"]),
                                UsuarioModificacion = rd["UsuarioModificacion"] as string
                            });
                        }

                        // RS#3: total
                        int total = 0;
                        if (await rd.NextResultAsync() && await rd.ReadAsync())
                            total = rd["Total"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Total"]);

                        return Ok(new { Resultado = 1, Mensaje = "OK", Pagina, TamanoPagina, Total = total, Items = items });
                    }
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error interno: " + ex.Message));
            }
        }

        // ===== CREAR =====
        // GET /Menus/Crear?Usuario=&IdModulo=&Nombre=&OrdenMenu=
        [HttpGet]
        [Route("Crear")]
        public async Task<IHttpActionResult> Crear(string Usuario, int IdModulo, string Nombre, int OrdenMenu)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario) || string.IsNullOrWhiteSpace(Nombre))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar Usuario, IdModulo, Nombre y OrdenMenu." });

                var u = Usuario.Trim();
                if (!await SeguridadHelper.TienePermisoAsync(u, Opciones.Menus, PermisoAccion.Alta))
                    return Denegado(PermisoAccion.Alta);

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Menu_Crear", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdModulo", SqlDbType.Int).Value = IdModulo;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 100).Value = Nombre.Trim();
                    cmd.Parameters.Add("@OrdenMenu", SqlDbType.Int).Value = OrdenMenu;
                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = u;

                    cn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        if (!await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "";
                        if (resultado != 1) return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        object data = null;
                        if (await rd.NextResultAsync() && await rd.ReadAsync())
                        {
                            data = new
                            {
                                IdMenu = Convert.ToInt32(rd["IdMenu"]),
                                IdModulo = rd["IdModulo"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["IdModulo"]),
                                Nombre = rd["Nombre"] as string,
                                OrdenMenu = rd["OrdenMenu"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["OrdenMenu"]),
                                FechaCreacion = Fmt(rd["FechaCreacion"]),
                                UsuarioCreacion = rd["UsuarioCreacion"] as string,
                                FechaModificacion = Fmt(rd["FechaModificacion"]),
                                UsuarioModificacion = rd["UsuarioModificacion"] as string
                            };
                        }

                        return Ok(new { Resultado = 1, Mensaje = mensaje, Data = data });
                    }
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error interno: " + ex.Message));
            }
        }

        // ===== ACTUALIZAR =====
        // GET /Menus/Actualizar?Usuario=&IdMenu=&IdModulo=&Nombre=&OrdenMenu=
        [HttpGet]
        [Route("Actualizar")]
        public async Task<IHttpActionResult> Actualizar(string Usuario, int IdMenu, int? IdModulo = null, string Nombre = null, int? OrdenMenu = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar Usuario." });

                var u = Usuario.Trim();
                if (!await SeguridadHelper.TienePermisoAsync(u, Opciones.Menus, PermisoAccion.Cambio))
                    return Denegado(PermisoAccion.Cambio);

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Menu_Actualizar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdMenu", SqlDbType.Int).Value = IdMenu;
                    cmd.Parameters.Add("@IdModulo", SqlDbType.Int).Value = (object)IdModulo ?? DBNull.Value;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 100).Value =
                        (object)(string.IsNullOrWhiteSpace(Nombre) ? null : Nombre.Trim()) ?? DBNull.Value;
                    cmd.Parameters.Add("@OrdenMenu", SqlDbType.Int).Value = (object)OrdenMenu ?? DBNull.Value;
                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = u;

                    cn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        if (!await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "";
                        if (resultado != 1) return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        object data = null;
                        if (await rd.NextResultAsync() && await rd.ReadAsync())
                        {
                            data = new
                            {
                                IdMenu = Convert.ToInt32(rd["IdMenu"]),
                                IdModulo = rd["IdModulo"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["IdModulo"]),
                                Nombre = rd["Nombre"] as string,
                                OrdenMenu = rd["OrdenMenu"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["OrdenMenu"]),
                                FechaCreacion = Fmt(rd["FechaCreacion"]),
                                UsuarioCreacion = rd["UsuarioCreacion"] as string,
                                FechaModificacion = Fmt(rd["FechaModificacion"]),
                                UsuarioModificacion = rd["UsuarioModificacion"] as string
                            };
                        }

                        return Ok(new { Resultado = 1, Mensaje = mensaje, Data = data });
                    }
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error interno: " + ex.Message));
            }
        }

        // ===== ELIMINAR =====
        // GET /Menus/Eliminar?Usuario=&IdMenu=
        [HttpGet]
        [Route("Eliminar")]
        public async Task<IHttpActionResult> Eliminar(string Usuario, int IdMenu)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar Usuario." });

                var u = Usuario.Trim();
                if (!await SeguridadHelper.TienePermisoAsync(u, Opciones.Menus, PermisoAccion.Baja))
                    return Denegado(PermisoAccion.Baja);

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Menu_Eliminar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdMenu", SqlDbType.Int).Value = IdMenu;

                    cn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        if (!await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "";
                        if (resultado != 1) return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        return Ok(new { Resultado = 1, Mensaje = mensaje });
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
