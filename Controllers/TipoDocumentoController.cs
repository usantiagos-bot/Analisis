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
    [RoutePrefix("TiposDocumento")]
    public class TiposDocumentoController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;

        private static string Fmt(object dt)
            => (dt == DBNull.Value || dt == null) ? null : ((DateTime)dt).ToString("yyyy-MM-ddTHH:mm:ss");

        private IHttpActionResult Denegado(PermisoAccion acc)
            => Ok(new { Resultado = 0, Mensaje = $"Permiso denegado ({acc})." });

        private IHttpActionResult Denegado(string detalle)
            => Ok(new { Resultado = 0, Mensaje = $"Permiso denegado ({detalle})." });

        // ========= LISTAR (un registro por filtro) =========
        // GET /TiposDocumento/Listar?usuarioAccion=&IdTipoDocumento=&Nombre=&incluirAuditoria=false
        [HttpGet]
        [Route("Listar")]
        public async Task<IHttpActionResult> Listar(
            string usuarioAccion,
            int? IdTipoDocumento = null,
            string Nombre = null,
            bool incluirAuditoria = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(usuarioAccion))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar usuarioAccion." });

                if (IdTipoDocumento == null && string.IsNullOrWhiteSpace(Nombre))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar IdTipoDocumento o Nombre." });

                var u = usuarioAccion.Trim();
                var puede =
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposDeDocumentos, PermisoAccion.Imprimir) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposDeDocumentos, PermisoAccion.Exportar) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposDeDocumentos, PermisoAccion.Cambio) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposDeDocumentos, PermisoAccion.Alta) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposDeDocumentos, PermisoAccion.Baja);

                if (!puede) return Denegado("lectura");

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_TipoDocumento_Listar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdTipoDocumento", SqlDbType.Int).Value = (object)IdTipoDocumento ?? DBNull.Value;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 50).Value =
                        (object)(string.IsNullOrWhiteSpace(Nombre) ? null : Nombre.Trim()) ?? DBNull.Value;

                    cn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        // RS#1
                        if (!await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "OK";
                        if (resultado != 1) return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        // RS#2
                        if (!await rd.NextResultAsync() || !await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "No se encontraron datos." });

                        var data = new
                        {
                            IdTipoDocumento = Convert.ToInt32(rd["IdTipoDocumento"]),
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

        // ========= LISTAR BÚSQUEDA (paginado) =========
        // GET /TiposDocumento/ListarBusqueda?usuarioAccion=&Buscar=&Pagina=1&TamanoPagina=50&OrdenPor=Nombre&OrdenDir=ASC
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
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposDeDocumentos, PermisoAccion.Imprimir) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposDeDocumentos, PermisoAccion.Exportar) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposDeDocumentos, PermisoAccion.Cambio) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposDeDocumentos, PermisoAccion.Alta) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposDeDocumentos, PermisoAccion.Baja);

                if (!puede) return Denegado("lectura");

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_TipoDocumento_Listar_Busqueda", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@Buscar", SqlDbType.VarChar, 50).Value =
                        (object)(string.IsNullOrWhiteSpace(Buscar) ? null : Buscar.Trim()) ?? DBNull.Value;
                    cmd.Parameters.Add("@Page", SqlDbType.Int).Value = Pagina;
                    cmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = TamanoPagina;
                    cmd.Parameters.Add("@OrdenPor", SqlDbType.VarChar, 20).Value = string.IsNullOrWhiteSpace(OrdenPor) ? "Nombre" : OrdenPor.Trim();
                    cmd.Parameters.Add("@OrdenDir", SqlDbType.VarChar, 4).Value = string.Equals(OrdenDir, "ASC", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

                    cn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        // RS#1
                        if (!await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "OK";
                        if (resultado != 1) return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        // RS#2
                        if (!await rd.NextResultAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin datos." });

                        var items = new List<object>();
                        while (await rd.ReadAsync())
                        {
                            items.Add(new
                            {
                                IdTipoDocumento = Convert.ToInt32(rd["IdTipoDocumento"]),
                                Nombre = rd["Nombre"] as string,
                                FechaCreacion = Fmt(rd["FechaCreacion"]),
                                UsuarioCreacion = rd["UsuarioCreacion"] as string,
                                FechaModificacion = Fmt(rd["FechaModificacion"]),
                                UsuarioModificacion = rd["UsuarioModificacion"] as string
                            });
                        }

                        // RS#3
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
        // GET /TiposDocumento/Crear?Usuario=&Nombre=
        [HttpGet]
        [Route("Crear")]
        public async Task<IHttpActionResult> Crear(string Usuario, string Nombre)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario) || string.IsNullOrWhiteSpace(Nombre))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar Usuario y Nombre." });

                var u = Usuario.Trim();
                if (!await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposDeDocumentos, PermisoAccion.Alta))
                    return Denegado(PermisoAccion.Alta);

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_TipoDocumento_Crear", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 50).Value = Nombre.Trim();
                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = u;

                    cn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        // RS#1
                        if (!await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "";
                        if (resultado != 1) return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        // RS#2
                        object data = null;
                        if (await rd.NextResultAsync() && await rd.ReadAsync())
                        {
                            data = new
                            {
                                IdTipoDocumento = Convert.ToInt32(rd["IdTipoDocumento"]),
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
        // GET /TiposDocumento/Actualizar?Usuario=&IdTipoDocumento=&Nombre=
        [HttpGet]
        [Route("Actualizar")]
        public async Task<IHttpActionResult> Actualizar(string Usuario, int IdTipoDocumento, string Nombre = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar Usuario." });

                var u = Usuario.Trim();
                if (!await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposDeDocumentos, PermisoAccion.Cambio))
                    return Denegado(PermisoAccion.Cambio);

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_TipoDocumento_Actualizar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdTipoDocumento", SqlDbType.Int).Value = IdTipoDocumento;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 50).Value =
                        (object)(string.IsNullOrWhiteSpace(Nombre) ? null : Nombre.Trim()) ?? DBNull.Value;
                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = u;

                    cn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        // RS#1
                        if (!await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "";
                        if (resultado != 1) return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        // RS#2
                        object data = null;
                        if (await rd.NextResultAsync() && await rd.ReadAsync())
                        {
                            data = new
                            {
                                IdTipoDocumento = Convert.ToInt32(rd["IdTipoDocumento"]),
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
        // GET /TiposDocumento/Eliminar?Usuario=&IdTipoDocumento=
        [HttpGet]
        [Route("Eliminar")]
        public async Task<IHttpActionResult> Eliminar(string Usuario, int IdTipoDocumento)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar Usuario." });

                var u = Usuario.Trim();
                if (!await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposDeDocumentos, PermisoAccion.Baja))
                    return Denegado(PermisoAccion.Baja);

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_TipoDocumento_Eliminar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdTipoDocumento", SqlDbType.Int).Value = IdTipoDocumento;

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
