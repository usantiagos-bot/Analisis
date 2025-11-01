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
    [RoutePrefix("Cuentas")]
    public class CuentasController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;

        private static string Fmt(object dt)
            => (dt == DBNull.Value || dt == null) ? null : ((DateTime)dt).ToString("yyyy-MM-ddTHH:mm:ss");

        private IHttpActionResult Denegado(string detalle)
            => Ok(new { Resultado = 0, Mensaje = $"Permiso denegado ({detalle})." });

        // ========= OBTENER =========
        // GET /Cuentas/Obtener?usuarioAccion=&IdSaldoCuenta=
        [HttpGet]
        [Route("Obtener")]
        public async Task<IHttpActionResult> Obtener(string usuarioAccion, int? IdSaldoCuenta)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(usuarioAccion))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar usuarioAccion." });
                if (IdSaldoCuenta == null)
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar IdSaldoCuenta." });

                var u = usuarioAccion.Trim();
                var puede =
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDeCuentas, PermisoAccion.Imprimir) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDeCuentas, PermisoAccion.Exportar) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDeCuentas, PermisoAccion.Cambio) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDeCuentas, PermisoAccion.Alta) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDeCuentas, PermisoAccion.Baja);
                if (!puede) return Denegado("lectura");

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Cuenta_Obtener", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdSaldoCuenta", SqlDbType.Int).Value = IdSaldoCuenta.Value;

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

                        var cuenta = new
                        {
                            IdSaldoCuenta = Convert.ToInt32(rd["IdSaldoCuenta"]),
                            IdPersona = Convert.ToInt32(rd["IdPersona"]),
                            IdTipoSaldoCuenta = Convert.ToInt32(rd["IdTipoSaldoCuenta"]),
                            IdStatusCuenta = Convert.ToInt32(rd["IdStatusCuenta"]),
                            SaldoAnterior = Convert.ToDecimal(rd["SaldoAnterior"]),
                            Debitos = Convert.ToDecimal(rd["Debitos"]),
                            Creditos = Convert.ToDecimal(rd["Creditos"]),
                            FechaCreacion = Fmt(rd["FechaCreacion"]),
                            UsuarioCreacion = rd["UsuarioCreacion"] as string,
                            FechaModificacion = Fmt(rd["FechaModificacion"]),
                            UsuarioModificacion = rd["UsuarioModificacion"] as string
                        };

                        return Ok(new { Resultado = 1, Mensaje = "OK", Data = cuenta });
                    }
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error interno: " + ex.Message));
            }
        }

        // ========= LISTAR (paginado + filtros) =========
        // GET /Cuentas/ListarBusqueda?usuarioAccion=&Buscar=&IdPersona=&IdStatusCuenta=&IdTipoSaldoCuenta=&Pagina=1&TamanoPagina=50&OrdenPor=FechaCreacion&OrdenDir=DESC
        [HttpGet]
        [Route("ListarBusqueda")]
        public async Task<IHttpActionResult> ListarBusqueda(
            string usuarioAccion,
            string Buscar = null,
            int? IdPersona = null,
            int? IdStatusCuenta = null,
            int? IdTipoSaldoCuenta = null,
            int Pagina = 1,
            int TamanoPagina = 50,
            string OrdenPor = "FechaCreacion",
            string OrdenDir = "DESC")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(usuarioAccion))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar usuarioAccion." });

                var u = usuarioAccion.Trim();
                var puede =
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDeCuentas, PermisoAccion.Imprimir) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDeCuentas, PermisoAccion.Exportar) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDeCuentas, PermisoAccion.Cambio) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDeCuentas, PermisoAccion.Alta) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDeCuentas, PermisoAccion.Baja);
                if (!puede) return Denegado("lectura");

                // Normalizaciones simples
                OrdenDir = string.Equals(OrdenDir, "ASC", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
                OrdenPor = string.IsNullOrWhiteSpace(OrdenPor) ? "FechaCreacion" : OrdenPor.Trim();

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Cuenta_Listar_Busqueda", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@Buscar", SqlDbType.VarChar, 100).Value =
                        (object)(string.IsNullOrWhiteSpace(Buscar) ? null : Buscar.Trim()) ?? DBNull.Value;
                    cmd.Parameters.Add("@IdPersona", SqlDbType.Int).Value = (object)IdPersona ?? DBNull.Value;
                    cmd.Parameters.Add("@IdStatusCuenta", SqlDbType.Int).Value = (object)IdStatusCuenta ?? DBNull.Value;
                    cmd.Parameters.Add("@IdTipoSaldoCuenta", SqlDbType.Int).Value = (object)IdTipoSaldoCuenta ?? DBNull.Value;
                    cmd.Parameters.Add("@Pagina", SqlDbType.Int).Value = Pagina;
                    cmd.Parameters.Add("@TamanoPagina", SqlDbType.Int).Value = TamanoPagina;
                    cmd.Parameters.Add("@OrdenPor", SqlDbType.VarChar, 20).Value = OrdenPor;
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
                                IdSaldoCuenta = Convert.ToInt32(rd["IdSaldoCuenta"]),
                                IdPersona = Convert.ToInt32(rd["IdPersona"]),
                                IdTipoSaldoCuenta = Convert.ToInt32(rd["IdTipoSaldoCuenta"]),
                                IdStatusCuenta = Convert.ToInt32(rd["IdStatusCuenta"]),
                                SaldoAnterior = Convert.ToDecimal(rd["SaldoAnterior"]),
                                Debitos = Convert.ToDecimal(rd["Debitos"]),
                                Creditos = Convert.ToDecimal(rd["Creditos"]),
                                FechaCreacion = Fmt(rd["FechaCreacion"]),
                                UsuarioCreacion = rd["UsuarioCreacion"] as string,
                                FechaModificacion = Fmt(rd["FechaModificacion"]),
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

        // ========= CREAR =========
        // POST /Cuentas/Crear
        // body: Usuario (req), IdPersona (req), IdTipoSaldoCuenta (req), [IdStatusCuenta], [SaldoAnterior]
        [HttpPost]
        [Route("Crear")]
        public async Task<IHttpActionResult> Crear(
            string Usuario,
            int IdPersona,
            int IdTipoSaldoCuenta,
            int? IdStatusCuenta = null,
            decimal? SaldoAnterior = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar Usuario." });

                var u = Usuario.Trim();
                if (!await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDeCuentas, PermisoAccion.Alta))
                    return Denegado("alta");

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Cuenta_Crear", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdPersona", SqlDbType.Int).Value = IdPersona;
                    cmd.Parameters.Add("@IdTipoSaldoCuenta", SqlDbType.Int).Value = IdTipoSaldoCuenta;
                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = u;
                    cmd.Parameters.Add("@IdStatusCuenta", SqlDbType.Int).Value = (object)IdStatusCuenta ?? DBNull.Value;
                    cmd.Parameters.Add("@SaldoAnterior", SqlDbType.Decimal).Value = (object)SaldoAnterior ?? DBNull.Value;

                    cn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        if (!await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "OK";
                        if (resultado != 1) return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        object cuenta = null;
                        if (await rd.NextResultAsync() && await rd.ReadAsync())
                        {
                            cuenta = new
                            {
                                IdSaldoCuenta = Convert.ToInt32(rd["IdSaldoCuenta"]),
                                IdPersona = Convert.ToInt32(rd["IdPersona"]),
                                IdTipoSaldoCuenta = Convert.ToInt32(rd["IdTipoSaldoCuenta"]),
                                IdStatusCuenta = Convert.ToInt32(rd["IdStatusCuenta"]),
                                SaldoAnterior = Convert.ToDecimal(rd["SaldoAnterior"]),
                                Debitos = Convert.ToDecimal(rd["Debitos"]),
                                Creditos = Convert.ToDecimal(rd["Creditos"]),
                                FechaCreacion = Fmt(rd["FechaCreacion"]),
                                UsuarioCreacion = rd["UsuarioCreacion"] as string
                            };
                        }

                        return Ok(new { Resultado = 1, Mensaje = mensaje, Data = cuenta });
                    }
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error interno: " + ex.Message));
            }
        }

        // ========= ACTUALIZAR =========
        // POST /Cuentas/Actualizar
        // body: IdSaldoCuenta (req), Usuario (req), [IdStatusCuenta], [SaldoAnterior]
        [HttpPost]
        [Route("Actualizar")]
        public async Task<IHttpActionResult> Actualizar(
            int IdSaldoCuenta,
            string Usuario,
            int? IdStatusCuenta = null,
            decimal? SaldoAnterior = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar Usuario." });

                var u = Usuario.Trim();
                if (!await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDeCuentas, PermisoAccion.Cambio))
                    return Denegado("cambio");

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Cuenta_Actualizar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdSaldoCuenta", SqlDbType.Int).Value = IdSaldoCuenta;
                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = u;
                    cmd.Parameters.Add("@IdStatusCuenta", SqlDbType.Int).Value = (object)IdStatusCuenta ?? DBNull.Value;
                    cmd.Parameters.Add("@SaldoAnterior", SqlDbType.Decimal).Value = (object)SaldoAnterior ?? DBNull.Value;

                    cn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        if (!await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "OK";
                        if (resultado != 1) return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        object cuenta = null;
                        if (await rd.NextResultAsync() && await rd.ReadAsync())
                        {
                            cuenta = new
                            {
                                IdSaldoCuenta = Convert.ToInt32(rd["IdSaldoCuenta"]),
                                IdPersona = Convert.ToInt32(rd["IdPersona"]),
                                IdTipoSaldoCuenta = Convert.ToInt32(rd["IdTipoSaldoCuenta"]),
                                IdStatusCuenta = Convert.ToInt32(rd["IdStatusCuenta"]),
                                SaldoAnterior = Convert.ToDecimal(rd["SaldoAnterior"]),
                                Debitos = Convert.ToDecimal(rd["Debitos"]),
                                Creditos = Convert.ToDecimal(rd["Creditos"]),
                                FechaCreacion = Fmt(rd["FechaCreacion"]),
                                UsuarioCreacion = rd["UsuarioCreacion"] as string,
                                FechaModificacion = Fmt(rd["FechaModificacion"]),
                                UsuarioModificacion = rd["UsuarioModificacion"] as string
                            };
                        }

                        return Ok(new { Resultado = 1, Mensaje = mensaje, Data = cuenta });
                    }
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error interno: " + ex.Message));
            }
        }

        // ========= SALDO ACTUAL (vista) =========
        // GET /Cuentas/SaldoActual?IdSaldoCuenta=
        [HttpGet]
        [Route("SaldoActual")]
        public async Task<IHttpActionResult> SaldoActual(int IdSaldoCuenta)
        {
            try
            {
                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("SELECT IdSaldoCuenta, SaldoAnterior, Debitos, Creditos, (SaldoAnterior + Debitos - Creditos) AS SaldoActual FROM dbo.vw_SaldoCuenta WHERE IdSaldoCuenta = @Id", cn))
                {
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = IdSaldoCuenta;
                    cn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        if (!await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Cuenta no encontrada." });

                        var saldo = new
                        {
                            IdSaldoCuenta = Convert.ToInt32(rd["IdSaldoCuenta"]),
                            SaldoAnterior = Convert.ToDecimal(rd["SaldoAnterior"]),
                            Debitos = Convert.ToDecimal(rd["Debitos"]),
                            Creditos = Convert.ToDecimal(rd["Creditos"]),
                            SaldoActual = Convert.ToDecimal(rd["SaldoActual"])
                        };

                        return Ok(new { Resultado = 1, Mensaje = "OK", Data = saldo });
                    }
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error interno: " + ex.Message));
            }
        }

        // ========= DOCUMENTOS DEL CLIENTE (vista) =========
        // GET /Cuentas/DocumentosPersona?IdPersona=
        [HttpGet]
        [Route("DocumentosPersona")]
        public async Task<IHttpActionResult> DocumentosPersona(int IdPersona)
        {
            try
            {
                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand(@"SELECT IdPersona, IdTipoDocumento, TipoDocumento, NoDocumento, FechaCreacion, UsuarioCreacion, FechaModificacion, UsuarioModificacion 
                                                  FROM dbo.vw_DocumentosPersona WHERE IdPersona = @p", cn))
                {
                    cmd.Parameters.Add("@p", SqlDbType.Int).Value = IdPersona;
                    cn.Open();

                    var items = new List<object>();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        while (await rd.ReadAsync())
                        {
                            items.Add(new
                            {
                                IdPersona = Convert.ToInt32(rd["IdPersona"]),
                                IdTipoDocumento = Convert.ToInt32(rd["IdTipoDocumento"]),
                                TipoDocumento = rd["TipoDocumento"] as string,
                                NoDocumento = rd["NoDocumento"] as string,
                                FechaCreacion = Fmt(rd["FechaCreacion"]),
                                UsuarioCreacion = rd["UsuarioCreacion"] as string,
                                FechaModificacion = Fmt(rd["FechaModificacion"]),
                                UsuarioModificacion = rd["UsuarioModificacion"] as string
                            });
                        }
                    }

                    return Ok(new { Resultado = 1, Mensaje = "OK", Items = items });
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error interno: " + ex.Message));
            }
        }

        // ========= LISTAR MOVIMIENTOS (paginado + fecha) =========
        // GET /Cuentas/ListarMovimientos?usuarioAccion=&IdSaldoCuenta=&Desde=&Hasta=&Pagina=1&TamanoPagina=50&OrdenDir=DESC
        [HttpGet]
        [Route("ListarMovimientos")]
        public async Task<IHttpActionResult> ListarMovimientos(
            string usuarioAccion,
            int IdSaldoCuenta,
            DateTime? Desde = null,
            DateTime? Hasta = null,
            int Pagina = 1,
            int TamanoPagina = 50,
            string OrdenDir = "DESC")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(usuarioAccion))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar usuarioAccion." });

                var u = usuarioAccion.Trim();
                var puede =
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDeCuentas, PermisoAccion.Imprimir) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDeCuentas, PermisoAccion.Exportar);
                if (!puede) return Denegado("lectura");

                OrdenDir = string.Equals(OrdenDir, "ASC", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Movimiento_Listar", cn)) // :contentReference[oaicite:2]{index=2}
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdSaldoCuenta", SqlDbType.Int).Value = IdSaldoCuenta;
                    cmd.Parameters.Add("@Desde", SqlDbType.DateTime).Value = (object)Desde ?? DBNull.Value;
                    cmd.Parameters.Add("@Hasta", SqlDbType.DateTime).Value = (object)Hasta ?? DBNull.Value;
                    cmd.Parameters.Add("@Pagina", SqlDbType.Int).Value = Pagina;
                    cmd.Parameters.Add("@TamanoPagina", SqlDbType.Int).Value = TamanoPagina;
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
                                IdMovimientoCuenta = Convert.ToInt32(rd["IdMovimientoCuenta"]),
                                IdSaldoCuenta = Convert.ToInt32(rd["IdSaldoCuenta"]),
                                IdTipoMovimientoCXC = Convert.ToInt32(rd["IdTipoMovimientoCXC"]),
                                FechaMovimiento = Fmt(rd["FechaMovimiento"]),
                                ValorMovimiento = Convert.ToDecimal(rd["ValorMovimiento"]),
                                GeneradoAutomaticamente = rd["GeneradoAutomaticamente"] != DBNull.Value ? Convert.ToBoolean(rd["GeneradoAutomaticamente"]) : (bool?)null,
                                Descripcion = rd["Descripcion"] as string,
                                FechaCreacion = Fmt(rd["FechaCreacion"]),
                                UsuarioCreacion = rd["UsuarioCreacion"] as string
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

        // GET /Cuentas/ConsultaSaldos?usuarioAccion=&Buscar=&IdPersona=&IdSaldoCuenta=&Desde=&Hasta=&Modo=porCuenta&Pagina=1&TamanoPagina=50
        [HttpGet]
        [Route("ConsultaSaldos")]
        public async Task<IHttpActionResult> ConsultaSaldos(
            string usuarioAccion,
            string Buscar = null,
            int? IdPersona = null,
            int? IdSaldoCuenta = null,
            DateTime? Desde = null,
            DateTime? Hasta = null,
            string Modo = "porCuenta",    // 'porCuenta' | 'porCliente'
            int Pagina = 1,
            int TamanoPagina = 50)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(usuarioAccion))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar usuarioAccion." });

                var u = usuarioAccion.Trim();
                var puede =
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDeCuentas, PermisoAccion.Imprimir) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDeCuentas, PermisoAccion.Exportar) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDeCuentas, PermisoAccion.Cambio) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDeCuentas, PermisoAccion.Alta) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDeCuentas, PermisoAccion.Baja);
                if (!puede) return Denegado("lectura");

                // Normaliza Modo
                Modo = string.Equals(Modo, "porCliente", StringComparison.OrdinalIgnoreCase) ? "porCliente" : "porCuenta";

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Cuenta_ConsultaSaldos", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@Buscar", SqlDbType.VarChar, 100).Value =
                        (object)(string.IsNullOrWhiteSpace(Buscar) ? null : Buscar.Trim()) ?? DBNull.Value;
                    cmd.Parameters.Add("@IdPersona", SqlDbType.Int).Value = (object)IdPersona ?? DBNull.Value;
                    cmd.Parameters.Add("@IdSaldoCuenta", SqlDbType.Int).Value = (object)IdSaldoCuenta ?? DBNull.Value;
                    cmd.Parameters.Add("@Desde", SqlDbType.DateTime).Value = (object)Desde ?? DBNull.Value;
                    cmd.Parameters.Add("@Hasta", SqlDbType.DateTime).Value = (object)Hasta ?? DBNull.Value;
                    cmd.Parameters.Add("@Modo", SqlDbType.VarChar, 12).Value = Modo;
                    cmd.Parameters.Add("@Pagina", SqlDbType.Int).Value = Pagina;
                    cmd.Parameters.Add("@TamanoPagina", SqlDbType.Int).Value = TamanoPagina;

                    cn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        if (!await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "OK";
                        if (resultado != 1) return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        // Resultset 2: filas
                        if (!await rd.NextResultAsync())
                            return Ok(new { Resultado = 1, Mensaje = "OK", Pagina, TamanoPagina, Total = 0, Items = new object[0] });

                        var items = new List<object>();
                        while (await rd.ReadAsync())
                        {
                            items.Add(new
                            {
                                Modo = rd["Modo"] as string,
                                IdSaldoCuenta = rd["IdSaldoCuenta"] != DBNull.Value ? Convert.ToInt32(rd["IdSaldoCuenta"]) : (int?)null,
                                IdPersona = Convert.ToInt32(rd["IdPersona"]),
                                NombreCompleto = rd["NombreCompleto"] as string,
                                SaldoInicial = Convert.ToDecimal(rd["SaldoInicial"]),
                                Cargos = Convert.ToDecimal(rd["Cargos"]),
                                Abonos = Convert.ToDecimal(rd["Abonos"]),
                                SaldoFinal = Convert.ToDecimal(rd["SaldoFinal"])
                            });
                        }

                        // Resultset 3: total
                        int total = 0;
                        if (await rd.NextResultAsync() && await rd.ReadAsync())
                            total = rd["Total"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Total"]);

                        return Ok(new
                        {
                            Resultado = 1,
                            Mensaje = "OK",
                            Filtros = new
                            {
                                Buscar = string.IsNullOrWhiteSpace(Buscar) ? null : Buscar.Trim(),
                                IdPersona,
                                IdSaldoCuenta,
                                Desde = Fmt(Desde),
                                Hasta = Fmt(Hasta),
                                Modo
                            },
                            Pagina,
                            TamanoPagina,
                            Total = total,
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

    }
}
