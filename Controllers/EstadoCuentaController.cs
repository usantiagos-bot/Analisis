using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using ProyectoAnalisis.Helpers;
using ProyectoAnalisis.Permissions;

namespace ProyectoAnalisis.Controllers
{
    [RoutePrefix("EstadoCuenta")]
    public class EstadoCuentaController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;
        private static string Fmt(object dt) => (dt == DBNull.Value || dt == null) ? null : ((DateTime)dt).ToString("yyyy-MM-ddTHH:mm:ss");
        private IHttpActionResult Denegado(string d) => Ok(new { Resultado = 0, Mensaje = $"Permiso denegado ({d})." });

        // GET /EstadoCuenta/Consultar?usuarioAccion=&IdSaldoCuenta=&IdPersona=&Nombre=&Apellido=&Desde=2025-09-01&Hasta=2025-09-30&Pagina=1&TamanoPagina=200&OrdenDir=ASC
        [HttpGet]
        [Route("Consultar")]
        public async Task<IHttpActionResult> Consultar(
            string usuarioAccion,
            int? IdSaldoCuenta = null,
            int? IdPersona = null,
            string Nombre = null,
            string Apellido = null,
            DateTime? Desde = null,
            DateTime? Hasta = null,
            int Pagina = 1,
            int TamanoPagina = 200,
            string OrdenDir = "ASC")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(usuarioAccion))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar usuarioAccion." });

                var u = usuarioAccion.Trim();
                var puede =
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.EstadoDeCuentas, PermisoAccion.Imprimir) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.EstadoDeCuentas, PermisoAccion.Exportar) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.EstadoDeCuentas, PermisoAccion.Cambio) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.EstadoDeCuentas, PermisoAccion.Alta) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.EstadoDeCuentas, PermisoAccion.Baja);

                if (!puede) return Denegado("lectura");

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_EstadoCuenta_Consultar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdSaldoCuenta", SqlDbType.Int).Value = (object)IdSaldoCuenta ?? DBNull.Value;
                    cmd.Parameters.Add("@IdPersona", SqlDbType.Int).Value = (object)IdPersona ?? DBNull.Value;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 50).Value = (object)Nombre ?? DBNull.Value;
                    cmd.Parameters.Add("@Apellido", SqlDbType.VarChar, 50).Value = (object)Apellido ?? DBNull.Value;
                    cmd.Parameters.Add("@Desde", SqlDbType.Date).Value = (object)Desde ?? DBNull.Value;
                    cmd.Parameters.Add("@Hasta", SqlDbType.Date).Value = (object)Hasta ?? DBNull.Value;
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

                        // RS#2 Encabezado
                        if (!await rd.NextResultAsync() || !await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin encabezado." });

                        var header = new
                        {
                            IdSaldoCuenta = Convert.ToInt32(rd["IdSaldoCuenta"]),
                            IdPersona = Convert.ToInt32(rd["IdPersona"]),
                            Nombre = rd["Nombre"] as string,
                            Apellido = rd["Apellido"] as string,
                            CorreoElectronico = rd["CorreoElectronico"] as string,
                            Telefono = rd["Telefono"] as string,
                            TipoCuenta = rd["TipoCuenta"] as string,
                            StatusCuenta = rd["StatusCuenta"] as string,
                            PeriodoDesde = Fmt(rd["PeriodoDesde"]),
                            PeriodoHasta = Fmt(rd["PeriodoHasta"]),
                            SaldoAnterior = Convert.ToDecimal(rd["SaldoAnterior"]),
                            SaldoInicialPeriodo = Convert.ToDecimal(rd["SaldoInicialPeriodo"])
                        };

                        // RS#3 Items
                        var items = new List<object>();
                        if (await rd.NextResultAsync())
                        {
                            while (await rd.ReadAsync())
                            {
                                items.Add(new
                                {
                                    IdMovimientoCuenta = Convert.ToInt32(rd["IdMovimientoCuenta"]),
                                    FechaMovimiento = Fmt(rd["FechaMovimiento"]),
                                    TipoMovimiento = rd["TipoMovimiento"] as string,
                                    DocumentoRef = rd["DocumentoRef"] as string,
                                    Cargo = Convert.ToDecimal(rd["Cargo"]),
                                    Abono = Convert.ToDecimal(rd["Abono"]),
                                    SaldoAcumulado = Convert.ToDecimal(rd["SaldoAcumulado"])
                                });
                            }
                        }

                        // RS#4 Totales
                        object totales = null;
                        if (await rd.NextResultAsync() && await rd.ReadAsync())
                        {
                            totales = new
                            {
                                TotalCargos = Convert.ToDecimal(rd["TotalCargos"]),
                                TotalAbonos = Convert.ToDecimal(rd["TotalAbonos"]),
                                SaldoInicial = Convert.ToDecimal(rd["SaldoInicial"]),
                                SaldoFinal = Convert.ToDecimal(rd["SaldoFinal"])
                            };
                        }

                        return Ok(new { Resultado = 1, Mensaje = "OK", Header = header, Items = items, Totales = totales });
                    }
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error interno: " + ex.Message));
            }
        }

        // GET /EstadoCuenta/ExportarCsv?... (mismos filtros)
        [HttpGet]
        [Route("ExportarCsv")]
        public async Task<IHttpActionResult> ExportarCsv(
            string usuarioAccion,
            int? IdSaldoCuenta = null,
            int? IdPersona = null,
            string Nombre = null,
            string Apellido = null,
            DateTime? Desde = null,
            DateTime? Hasta = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(usuarioAccion))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar usuarioAccion." });

                var u = usuarioAccion.Trim();
                var puede =
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.EstadoDeCuentas, PermisoAccion.Exportar) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.EstadoDeCuentas, PermisoAccion.Imprimir);

                if (!puede) return Denegado("exportar");

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_EstadoCuenta_Exportar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdSaldoCuenta", SqlDbType.Int).Value = (object)IdSaldoCuenta ?? DBNull.Value;
                    cmd.Parameters.Add("@IdPersona", SqlDbType.Int).Value = (object)IdPersona ?? DBNull.Value;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 50).Value = (object)Nombre ?? DBNull.Value;
                    cmd.Parameters.Add("@Apellido", SqlDbType.VarChar, 50).Value = (object)Apellido ?? DBNull.Value;
                    cmd.Parameters.Add("@Desde", SqlDbType.Date).Value = (object)Desde ?? DBNull.Value;
                    cmd.Parameters.Add("@Hasta", SqlDbType.Date).Value = (object)Hasta ?? DBNull.Value;

                    cn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        if (!await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        if (resultado != 1) return Ok(new { Resultado = resultado, Mensaje = "Error al exportar." });

                        if (!await rd.NextResultAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin datos para exportar." });

                        var sb = new StringBuilder();
                        sb.AppendLine("IdMovimiento,Fecha,TipoMovimiento,DocumentoRef,Cargo,Abono,SaldoAcumulado");

                        while (await rd.ReadAsync())
                        {
                            sb.AppendFormat("{0},{1},\"{2}\",\"{3}\",{4},{5},{6}\r\n",
                                rd["IdMovimientoCuenta"],
                                Convert.ToDateTime(rd["FechaMovimiento"]).ToString("yyyy-MM-dd HH:mm:ss"),
                                (rd["TipoMovimiento"] as string ?? "").Replace("\"", "\"\""),
                                (rd["DocumentoRef"] as string ?? "").Replace("\"", "\"\""),
                                rd["Cargo"], rd["Abono"], rd["SaldoAcumulado"]);
                        }

                        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                        var result = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                        {
                            Content = new System.Net.Http.ByteArrayContent(bytes)
                        };
                        result.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
                        result.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                        {
                            FileName = "EstadoCuenta.csv"
                        };
                        return ResponseMessage(result);
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
