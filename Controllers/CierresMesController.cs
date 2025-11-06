using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Web.Http;
using ProyectoAnalisis.Helpers;      // SeguridadHelper, Opciones, PermisoAccion
using ProyectoAnalisis.Permissions;  // Enum de permisos

namespace ProyectoAnalisis.Controllers
{
    [RoutePrefix("CierreMes")]
    public class CierreMesController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;
        private static string Fmt(object dt) => (dt == DBNull.Value || dt == null) ? null : ((DateTime)dt).ToString("yyyy-MM-ddTHH:mm:ss");
        private IHttpActionResult Denegado(string d) => Ok(new { Resultado = 0, Mensaje = $"Permiso denegado ({d})." });

        // ============================================================
        // POST /CierreMes/Ejecutar?usuarioAccion=&Anio=&Mes=
        // Llama a dbo.sp_CierreMes_Ejecutar (@Anio, @Mes, @Usuario)
        // ============================================================
        [HttpPost]
        [Route("Ejecutar")]
        public async Task<IHttpActionResult> Ejecutar(string usuarioAccion, int Anio, int Mes)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(usuarioAccion))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar usuarioAccion." });

                var u = usuarioAccion.Trim();

                // Requiere permiso de proceso/cambio
                var puede =
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.CierreDeMes, PermisoAccion.Cambio) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.CierreDeMes, PermisoAccion.Alta);
                if (!puede) return Denegado("cierre de mes");

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_CierreMes_Ejecutar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@Anio", SqlDbType.Int).Value = Anio;
                    cmd.Parameters.Add("@Mes", SqlDbType.Int).Value = Mes;
                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = u;

                    cn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        if (!await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "OK";

                        // Opcional: segundo resultset con conteo/registros del histórico del período
                        object resumen = null;
                        if (await rd.NextResultAsync() && await rd.ReadAsync())
                        {
                            resumen = new
                            {
                                Anio = rd["Anio"] != DBNull.Value ? Convert.ToInt32(rd["Anio"]) : Anio,
                                Mes = rd["Mes"] != DBNull.Value ? Convert.ToInt32(rd["Mes"]) : Mes,
                                Registros = rd["Registros"] != DBNull.Value ? Convert.ToInt32(rd["Registros"]) : 0
                            };
                        }

                        return Ok(new { Resultado = resultado, Mensaje = mensaje, Resumen = resumen });
                    }
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error interno: " + ex.Message));
            }
        }

        // ============================================================
        // GET /CierreMes/PeriodosPendientes
        // Lista los períodos de PERIODO_CIERRE_MES con FechaCierre = NULL
        // ============================================================
        [HttpGet]
        [Route("PeriodosPendientes")]
        public async Task<IHttpActionResult> PeriodosPendientes()
        {
            try
            {
                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand(@"
                    SELECT Anio, Mes, FechaCierre = NULL
                    FROM dbo.PERIODO_CIERRE_MES
                    WHERE FechaCierre IS NULL
                    ORDER BY Anio DESC, Mes DESC;", cn))
                {
                    cn.Open();
                    var items = new List<object>();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        while (await rd.ReadAsync())
                        {
                            items.Add(new
                            {
                                Anio = Convert.ToInt32(rd["Anio"]),
                                Mes = Convert.ToInt32(rd["Mes"]),
                                FechaCierre = (string)null
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

        // ============================================================
        // GET /CierreMes/UltimosCierres?top=12
        // Consulta últimos períodos cerrados (informativo)
        // ============================================================
        [HttpGet]
        [Route("UltimosCierres")]
        public async Task<IHttpActionResult> UltimosCierres(int top = 12)
        {
            try
            {
                if (top <= 0) top = 12;

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand($@"
                    SELECT TOP (@top) Anio, Mes, FechaCierre
                    FROM dbo.PERIODO_CIERRE_MES
                    WHERE FechaCierre IS NOT NULL
                    ORDER BY FechaCierre DESC;", cn))
                {
                    cmd.Parameters.Add("@top", SqlDbType.Int).Value = top;

                    cn.Open();
                    var items = new List<object>();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        while (await rd.ReadAsync())
                        {
                            items.Add(new
                            {
                                Anio = Convert.ToInt32(rd["Anio"]),
                                Mes = Convert.ToInt32(rd["Mes"]),
                                FechaCierre = Fmt(rd["FechaCierre"])
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

        // ============================================================
        // POST /CierreMes/AbrirPeriodo?usuarioAccion=&Anio=&Mes=
        // (Opcional) Crea un período pendiente si no existe ya.
        // ============================================================
        [HttpPost]
        [Route("AbrirPeriodo")]
        public async Task<IHttpActionResult> AbrirPeriodo(string usuarioAccion, int Anio, int Mes)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(usuarioAccion))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar usuarioAccion." });

                var u = usuarioAccion.Trim();
                var puede =
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.CierreDeMes, PermisoAccion.Alta) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.CierreDeMes, PermisoAccion.Cambio);
                if (!puede) return Denegado("abrir período");

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand(@"
                    IF NOT EXISTS (SELECT 1 FROM dbo.PERIODO_CIERRE_MES WHERE Anio=@Anio AND Mes=@Mes)
                      INSERT INTO dbo.PERIODO_CIERRE_MES (Anio, Mes, FechaCierre)
                      VALUES (@Anio, @Mes, NULL);", cn))
                {
                    cmd.Parameters.Add("@Anio", SqlDbType.Int).Value = Anio;
                    cmd.Parameters.Add("@Mes", SqlDbType.Int).Value = Mes;

                    cn.Open();
                    await cmd.ExecuteNonQueryAsync();

                    return Ok(new { Resultado = 1, Mensaje = "Período abierto (o ya existía pendiente)." });
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error interno: " + ex.Message));
            }
        }
    }
}
