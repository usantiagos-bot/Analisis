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
    [RoutePrefix("CierreMes")]
    public class CierreMesController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;

        private IHttpActionResult Denegado(string detalle)
            => Ok(new { Resultado = 0, Mensaje = $"Permiso denegado ({detalle})." });

        // =========================
        // GET /CierreMes/Pendientes
        // Lista de períodos Anio/Mes con FechaCierre NULL
        // =========================
        [HttpGet]
        [Route("Pendientes")]
        public async Task<IHttpActionResult> Pendientes(string usuarioAccion)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(usuarioAccion))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar usuarioAccion." });

                var u = usuarioAccion.Trim();
                var puede =
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.CierreDeMes, PermisoAccion.Imprimir) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.CierreDeMes, PermisoAccion.Exportar) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.CierreDeMes, PermisoAccion.Cambio);

                if (!puede) return Denegado("lectura");

                var items = new List<object>();

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand(@"
                        SELECT Anio, Mes, FechaCierre
                        FROM dbo.PERIODO_CIERRE_MES WITH (NOLOCK)
                        WHERE FechaCierre IS NULL
                        ORDER BY Anio DESC, Mes DESC;", cn))
                {
                    await cn.OpenAsync();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        while (await rd.ReadAsync())
                        {
                            items.Add(new
                            {
                                Anio = Convert.ToInt32(rd["Anio"]),
                                Mes = Convert.ToInt32(rd["Mes"]),
                                FechaCierre = rd["FechaCierre"] == DBNull.Value ? null
                                              : ((DateTime)rd["FechaCierre"]).ToString("yyyy-MM-ddTHH:mm:ss")
                            });
                        }
                    }
                }

                return Ok(new { Resultado = 1, Mensaje = "OK", Items = items });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error interno: " + ex.Message));
            }
        }

        // =========================
        // POST /CierreMes/Ejecutar
        // Body (x-www-form-urlencoded o JSON):
        //   Usuario (req), Anio (req), Mes (req 1..12)
        // Lee los 2 result sets del SP:
        //   RS#1: Resultado, Mensaje
        //   RS#2: { PeriodoAnio, PeriodoMes, HistoricosInsertados, CuentasProcesadas }
        // =========================
        [HttpPost]
        [Route("Ejecutar")]
        public async Task<IHttpActionResult> Ejecutar(string Usuario, int Anio, int Mes)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar Usuario." });

                if (Mes < 1 || Mes > 12)
                    return Ok(new { Resultado = 0, Mensaje = "Mes debe estar entre 1 y 12." });

                var u = Usuario.Trim();
                var puede = await SeguridadHelper.TienePermisoAsync(u, Opciones.CierreDeMes, PermisoAccion.Cambio);
                if (!puede) return Denegado(PermisoAccion.Cambio.ToString());

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_CierreMes_Ejecutar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@Anio", SqlDbType.Int).Value = Anio;
                    cmd.Parameters.Add("@Mes", SqlDbType.Int).Value = Mes;
                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = u;

                    await cn.OpenAsync();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        // RS#1: meta
                        if (!await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        var resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        var mensaje = rd["Mensaje"] as string ?? "OK";

                        if (resultado != 1)
                            return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        // RS#2: detalle
                        object detalle = null;
                        if (await rd.NextResultAsync() && await rd.ReadAsync())
                        {
                            detalle = new
                            {
                                PeriodoAnio = rd["PeriodoAnio"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["PeriodoAnio"]),
                                PeriodoMes = rd["PeriodoMes"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["PeriodoMes"]),
                                HistoricosInsertados = rd["HistoricosInsertados"] == DBNull.Value ? 0 : Convert.ToInt32(rd["HistoricosInsertados"]),
                                CuentasProcesadas = rd["CuentasProcesadas"] == DBNull.Value ? 0 : Convert.ToInt32(rd["CuentasProcesadas"])
                            };
                        }

                        return Ok(new { Resultado = 1, Mensaje = mensaje, Data = detalle });
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
