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
    // Ejemplos de rutas:
    // GET  /TiposSaldoCuenta/Listar?usuarioAccion=...&IdTipoSaldoCuenta=&Nombre=&incluirAuditoria=false
    // GET  /TiposSaldoCuenta/ListarBusqueda?usuarioAccion=...&Buscar=&Pagina=1&TamanoPagina=20&OrdenPor=Nombre&OrdenDir=ASC
    // GET  /TiposSaldoCuenta/Crear?Usuario=...&Nombre=...
    // GET  /TiposSaldoCuenta/Actualizar?Usuario=...&IdTipoSaldoCuenta=...&Nombre=...
    // GET  /TiposSaldoCuenta/Eliminar?Usuario=...&IdTipoSaldoCuenta=...
    [RoutePrefix("TiposSaldoCuenta")]
    public class TiposSaldoCuentaController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;

        private static string Fmt(object dt)
            => (dt == DBNull.Value || dt == null) ? null : ((DateTime)dt).ToString("yyyy-MM-ddTHH:mm:ss");

        private IHttpActionResult Denegado(PermisoAccion acc)
            => Ok(new { Resultado = 0, Mensaje = $"Permiso denegado ({acc})." });

        private IHttpActionResult Denegado(string detalle)
            => Ok(new { Resultado = 0, Mensaje = $"Permiso denegado ({detalle})." });

        // ========= LISTAR (1 registro por filtro) =========
        // GET /TiposSaldoCuenta/Listar?usuarioAccion=&IdTipoSaldoCuenta=&Nombre=&incluirAuditoria=false
        [HttpGet]
        [Route("Listar")]
        public async Task<IHttpActionResult> Listar(
            string usuarioAccion,
            int? IdTipoSaldoCuenta = null,
            string Nombre = null,
            bool incluirAuditoria = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(usuarioAccion))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar usuarioAccion." });

                if (IdTipoSaldoCuenta == null && string.IsNullOrWhiteSpace(Nombre))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar IdTipoSaldoCuenta o Nombre." });

                var u = usuarioAccion.Trim();
                var puede =
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposDeCuentas, PermisoAccion.Imprimir) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposDeCuentas, PermisoAccion.Exportar) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposDeCuentas, PermisoAccion.Cambio) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposDeCuentas, PermisoAccion.Alta) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposDeCuentas, PermisoAccion.Baja);

                if (!puede) return Denegado("lectura");

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_TipoSaldoCuenta_Listar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdTipoSaldoCuenta", SqlDbType.Int).Value = (object)IdTipoSaldoCuenta ?? DBNull.Value;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 50).Value =
                        (object)(string.IsNullOrWhiteSpace(Nombre) ? null : Nombre.Trim()) ?? DBNull.Value;

                    cn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        // RS#1 meta
                        if (!await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "OK";
                        if (resultado != 1) return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        // RS#2 data (0..1)
                        if (!await rd.NextResultAsync() || !await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "No se encontraron datos." });

                        var data = new
                        {
                            IdTipoSaldoCuenta = Convert.ToInt32(rd["IdTipoSaldoCuenta"]),
                            Nombre = rd["Nombre"] as string,
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

        // ========= LISTAR BUSQUEDA (paginado) =========
        // GET /TiposSaldoCuenta/ListarBusqueda?usuarioAccion=&Buscar=&Pagina=1&TamanoPagina=50&OrdenPor=Nombre&OrdenDir=ASC
        [HttpGet]
        [Route("ListarBusqueda")]
        public async Task<IHttpActionResult> ListarBusqueda(
            string usuarioAccion,
            string Buscar = null,
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
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposDeCuentas, PermisoAccion.Imprimir) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposDeCuentas, PermisoAccion.Exportar) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposDeCuentas, PermisoAccion.Cambio) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposDeCuentas, PermisoAccion.Alta) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposDeCuentas, PermisoAccion.Baja);

                if (!puede) return Denegado("lectura");

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_TipoSaldoCuenta_Listar_Busqueda", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@Buscar", SqlDbType.VarChar, 100).Value =
                        (object)(string.IsNullOrWhiteSpace(Buscar) ? null : Buscar.Trim()) ?? DBNull.Value;
                    cmd.Parameters.Add("@Page", SqlDbType.Int).Value = Pagina;
                    cmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = TamanoPagina;
                    cmd.Parameters.Add("@OrdenPor", SqlDbType.VarChar, 20).Value = OrdenPor;
                    cmd.Parameters.Add("@OrdenDir", SqlDbType.VarChar, 4).Value = OrdenDir;

                    cn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        // RS#1 meta
                        if (!await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "OK";
                        if (resultado != 1) return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        // RS#2 items
                        if (!await rd.NextResultAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin datos." });

                        var items = new List<object>();
                        while (await rd.ReadAsync())
                        {
                            items.Add(new
                            {
                                IdTipoSaldoCuenta = Convert.ToInt32(rd["IdTipoSaldoCuenta"]),
                                Nombre = rd["Nombre"] as string,
                                FechaCreacion = Fmt(rd["FechaCreacion"]),
                                UsuarioCreacion = rd["UsuarioCreacion"] as string,
                                FechaModificacion = Fmt(rd["FechaModificacion"]),
                                UsuarioModificacion = rd["UsuarioModificacion"] as string
                            });
                        }

                        // RS#3 total
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

        // ========= CREAR =========
        // GET /TiposSaldoCuenta/Crear?Usuario=&Nombre=
        [HttpGet]
        [Route("Crear")]
        public async Task<IHttpActionResult> Crear(string Usuario, string Nombre)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario) || string.IsNullOrWhiteSpace(Nombre))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar Usuario y Nombre." });

                var u = Usuario.Trim();
                if (!await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposDeCuentas, PermisoAccion.Alta))
                    return Denegado(PermisoAccion.Alta);

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_TipoSaldoCuenta_Crear", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 50).Value = Nombre.Trim();
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
                                IdTipoSaldoCuenta = Convert.ToInt32(rd["IdTipoSaldoCuenta"]),
                                Nombre = rd["Nombre"] as string,
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

        // ========= ACTUALIZAR =========
        // GET /TiposSaldoCuenta/Actualizar?Usuario=&IdTipoSaldoCuenta=&Nombre=
        [HttpGet]
        [Route("Actualizar")]
        public async Task<IHttpActionResult> Actualizar(string Usuario, int IdTipoSaldoCuenta, string Nombre = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar Usuario." });

                var u = Usuario.Trim();
                if (!await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposDeCuentas, PermisoAccion.Cambio))
                    return Denegado(PermisoAccion.Cambio);

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_TipoSaldoCuenta_Actualizar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdTipoSaldoCuenta", SqlDbType.Int).Value = IdTipoSaldoCuenta;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 50).Value =
                        (object)(string.IsNullOrWhiteSpace(Nombre) ? null : Nombre.Trim()) ?? DBNull.Value;
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
                                IdTipoSaldoCuenta = Convert.ToInt32(rd["IdTipoSaldoCuenta"]),
                                Nombre = rd["Nombre"] as string,
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

        // ========= ELIMINAR =========
        // GET /TiposSaldoCuenta/Eliminar?Usuario=&IdTipoSaldoCuenta=
        [HttpGet]
        [Route("Eliminar")]
        public async Task<IHttpActionResult> Eliminar(string Usuario, int IdTipoSaldoCuenta)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar Usuario." });

                var u = Usuario.Trim();
                if (!await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposDeCuentas, PermisoAccion.Baja))
                    return Denegado(PermisoAccion.Baja);

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_TipoSaldoCuenta_Eliminar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdTipoSaldoCuenta", SqlDbType.Int).Value = IdTipoSaldoCuenta;

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
