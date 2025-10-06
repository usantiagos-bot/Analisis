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
    [RoutePrefix("TiposMovimientoCXC")]
    public class TiposMovimientoCXCController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;
        private static string F(object dt) => (dt == DBNull.Value || dt == null) ? null : ((DateTime)dt).ToString("yyyy-MM-ddTHH:mm:ss");

        private IHttpActionResult Denegado(PermisoAccion acc) => Ok(new { Resultado = 0, Mensaje = $"Permiso denegado ({acc})." });

        // ===== LISTAR (1 registro) =====
        // GET /TiposMovimientoCXC/Listar?usuarioAccion=&IdTipoMovimientoCXC=&Nombre=&incluirAuditoria=false
        [HttpGet]
        [Route("Listar")]
        public async Task<IHttpActionResult> Listar(string usuarioAccion, int? IdTipoMovimientoCXC = null, string Nombre = null, bool incluirAuditoria = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(usuarioAccion))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar usuarioAccion." });

                var u = usuarioAccion.Trim();
                var puede =
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposMovimientoCxc, PermisoAccion.Imprimir) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposMovimientoCxc, PermisoAccion.Exportar) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposMovimientoCxc, PermisoAccion.Cambio) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposMovimientoCxc, PermisoAccion.Alta) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposMovimientoCxc, PermisoAccion.Baja);
                if (!puede) return Denegado(PermisoAccion.Imprimir);

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_TipoMovimientoCXC_Listar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdTipoMovimientoCXC", SqlDbType.Int).Value = (object)IdTipoMovimientoCXC ?? DBNull.Value;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 75).Value =
                        (object)(string.IsNullOrWhiteSpace(Nombre) ? null : Nombre.Trim()) ?? DBNull.Value;
                    cmd.Parameters.Add("@IncluirAuditoria", SqlDbType.Bit).Value = incluirAuditoria;

                    cn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        if (!await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "OK";
                        if (resultado != 1) return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        if (!await rd.NextResultAsync() || !await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "No se encontraron datos." });

                        var data = new
                        {
                            IdTipoMovimientoCXC = Convert.ToInt32(rd["IdTipoMovimientoCXC"]),
                            Nombre = rd["Nombre"] as string,
                            OperacionCuentaCorriente = Convert.ToInt32(rd["OperacionCuentaCorriente"]),
                            FechaCreacion = F(rd["FechaCreacion"]),
                            UsuarioCreacion = incluirAuditoria ? rd["UsuarioCreacion"] as string : null,
                            FechaModificacion = incluirAuditoria ? F(rd["FechaModificacion"]) : null,
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

        // ===== LISTAR BUSQUEDA =====
        // GET /TiposMovimientoCXC/ListarBusqueda?usuarioAccion=&Buscar=&Pagina=1&TamanoPagina=20&OrdenPor=Nombre&OrdenDir=ASC
        [HttpGet]
        [Route("ListarBusqueda")]
        public async Task<IHttpActionResult> ListarBusqueda(string usuarioAccion, string Buscar = null, int Pagina = 1, int TamanoPagina = 20, string OrdenPor = "Nombre", string OrdenDir = "ASC")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(usuarioAccion))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar usuarioAccion." });

                var u = usuarioAccion.Trim();
                var puede =
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposMovimientoCxc, PermisoAccion.Imprimir) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposMovimientoCxc, PermisoAccion.Exportar) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposMovimientoCxc, PermisoAccion.Cambio) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposMovimientoCxc, PermisoAccion.Alta) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposMovimientoCxc, PermisoAccion.Baja);
                if (!puede) return Denegado(PermisoAccion.Imprimir);

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_TipoMovimientoCXC_Listar_Busqueda", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@Buscar", SqlDbType.VarChar, 100).Value =
                        (object)(string.IsNullOrWhiteSpace(Buscar) ? null : Buscar.Trim()) ?? DBNull.Value;
                    cmd.Parameters.Add("@Page", SqlDbType.Int).Value = Pagina;
                    cmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = TamanoPagina;
                    cmd.Parameters.Add("@OrdenPor", SqlDbType.VarChar, 30).Value = OrdenPor;
                    cmd.Parameters.Add("@OrdenDir", SqlDbType.VarChar, 4).Value = OrdenDir;

                    cn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        if (!await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "OK";
                        if (resultado != 1) return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        if (!await rd.NextResultAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin datos." });

                        var items = new List<object>();
                        while (await rd.ReadAsync())
                        {
                            items.Add(new
                            {
                                IdTipoMovimientoCXC = Convert.ToInt32(rd["IdTipoMovimientoCXC"]),
                                Nombre = rd["Nombre"] as string,
                                OperacionCuentaCorriente = Convert.ToInt32(rd["OperacionCuentaCorriente"]),
                                FechaCreacion = F(rd["FechaCreacion"]),
                                UsuarioCreacion = rd["UsuarioCreacion"] as string,
                                FechaModificacion = F(rd["FechaModificacion"]),
                                UsuarioModificacion = rd["UsuarioModificacion"] as string
                            });
                        }

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
        // GET /TiposMovimientoCXC/Crear?Usuario=&Nombre=&OperacionCuentaCorriente=
        [HttpGet]
        [Route("Crear")]
        public async Task<IHttpActionResult> Crear(string Usuario, string Nombre, int OperacionCuentaCorriente)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario) || string.IsNullOrWhiteSpace(Nombre))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar Usuario, Nombre y OperacionCuentaCorriente." });

                var u = Usuario.Trim();
                if (!await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposMovimientoCxc, PermisoAccion.Alta))
                    return Denegado(PermisoAccion.Alta);

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_TipoMovimientoCXC_Crear", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 75).Value = Nombre.Trim();
                    cmd.Parameters.Add("@OperacionCuentaCorriente", SqlDbType.Int).Value = OperacionCuentaCorriente;
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
                                IdTipoMovimientoCXC = Convert.ToInt32(rd["IdTipoMovimientoCXC"]),
                                Nombre = rd["Nombre"] as string,
                                OperacionCuentaCorriente = Convert.ToInt32(rd["OperacionCuentaCorriente"]),
                                FechaCreacion = F(rd["FechaCreacion"]),
                                UsuarioCreacion = rd["UsuarioCreacion"] as string,
                                FechaModificacion = F(rd["FechaModificacion"]),
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
        // GET /TiposMovimientoCXC/Actualizar?Usuario=&IdTipoMovimientoCXC=&Nombre=&OperacionCuentaCorriente=
        [HttpGet]
        [Route("Actualizar")]
        public async Task<IHttpActionResult> Actualizar(string Usuario, int IdTipoMovimientoCXC, string Nombre = null, int? OperacionCuentaCorriente = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar Usuario." });

                var u = Usuario.Trim();
                if (!await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposMovimientoCxc, PermisoAccion.Cambio))
                    return Denegado(PermisoAccion.Cambio);

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_TipoMovimientoCXC_Actualizar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdTipoMovimientoCXC", SqlDbType.Int).Value = IdTipoMovimientoCXC;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 75).Value =
                        (object)(string.IsNullOrWhiteSpace(Nombre) ? null : Nombre.Trim()) ?? DBNull.Value;
                    cmd.Parameters.Add("@OperacionCuentaCorriente", SqlDbType.Int).Value = (object)OperacionCuentaCorriente ?? DBNull.Value;
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
                                IdTipoMovimientoCXC = Convert.ToInt32(rd["IdTipoMovimientoCXC"]),
                                Nombre = rd["Nombre"] as string,
                                OperacionCuentaCorriente = Convert.ToInt32(rd["OperacionCuentaCorriente"]),
                                FechaCreacion = F(rd["FechaCreacion"]),
                                UsuarioCreacion = rd["UsuarioCreacion"] as string,
                                FechaModificacion = F(rd["FechaModificacion"]),
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
        // GET /TiposMovimientoCXC/Eliminar?Usuario=&IdTipoMovimientoCXC=
        [HttpGet]
        [Route("Eliminar")]
        public async Task<IHttpActionResult> Eliminar(string Usuario, int IdTipoMovimientoCXC)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar Usuario." });

                var u = Usuario.Trim();
                if (!await SeguridadHelper.TienePermisoAsync(u, Opciones.TiposMovimientoCxc, PermisoAccion.Baja))
                    return Denegado(PermisoAccion.Baja);

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_TipoMovimientoCXC_Eliminar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdTipoMovimientoCXC", SqlDbType.Int).Value = IdTipoMovimientoCXC;

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
