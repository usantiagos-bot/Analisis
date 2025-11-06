using System;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace Analisis.Controllers
{
    public class GrabacionMovimientoController : Controller
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;

        // ========== VISTA ==========
        // GET: /GrabacionMovimiento
        public ActionResult Index()
        {
            return View();
        }

        // ========== MODELOS (VM/DTO) ==========
        public class CrearMovimientoVm
        {
            [Required] public string Usuario { get; set; }
            [Required] public int IdSaldoCuenta { get; set; }
            [Required] public int IdTipoMovimientoCXC { get; set; }
            [Required] public DateTime FechaMovimiento { get; set; }
            [Required, Range(0.01, double.MaxValue, ErrorMessage = "El valor debe ser > 0")]
            public decimal ValorMovimiento { get; set; }

            [StringLength(50)] public string DocumentoRef { get; set; }
            [StringLength(150)] public string Descripcion { get; set; }
        }

        // ========== HELPERS ==========
        private static string Fmt(object dt)
            => (dt == DBNull.Value || dt == null) ? null : ((DateTime)dt).ToString("yyyy-MM-ddTHH:mm:ss");

        // ========== ENDPOINTS AUXILIARES ==========
        // GET: /GrabacionMovimiento/Tipos
        // Devuelve los tipos de movimiento para el combo
        [HttpGet]
        public async Task<JsonResult> Tipos()
        {
            try
            {
                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand(@"
                    SELECT IdTipoMovimientoCXC, Nombre, OperacionCuentaCorriente
                    FROM dbo.TIPO_MOVIMIENTO_CXC
                    ORDER BY Nombre;", cn))
                {
                    await cn.OpenAsync();
                    var list = new System.Collections.Generic.List<object>();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        while (await rd.ReadAsync())
                        {
                            list.Add(new
                            {
                                IdTipoMovimientoCXC = (int)rd["IdTipoMovimientoCXC"],
                                Nombre = rd["Nombre"] as string,
                                Operacion = (int)rd["OperacionCuentaCorriente"] // 1=CARGO, 2=ABONO
                            });
                        }
                    }
                    return Json(new { Resultado = 1, Items = list }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { Resultado = 0, Mensaje = "Error: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: /GrabacionMovimiento/CuentaActiva?idSaldoCuenta=5
        [HttpGet]
        public async Task<JsonResult> CuentaActiva(int idSaldoCuenta)
        {
            try
            {
                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 1 IdSaldoCuenta, IdStatusCuenta
                    FROM dbo.SALDO_CUENTA
                    WHERE IdSaldoCuenta = @p;", cn))
                {
                    cmd.Parameters.Add("@p", SqlDbType.Int).Value = idSaldoCuenta;
                    await cn.OpenAsync();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        if (!await rd.ReadAsync())
                            return Json(new { Resultado = 0, Mensaje = "Cuenta no encontrada." }, JsonRequestBehavior.AllowGet);

                        var status = (int)rd["IdStatusCuenta"];
                        var activa = (status == 1); // ajusta si tu catálogo usa otro id para ACTIVA
                        return Json(new { Resultado = 1, Activa = activa }, JsonRequestBehavior.AllowGet);
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { Resultado = 0, Mensaje = "Error: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // ========== TRANSACCIÓN PRINCIPAL ==========
        // POST: /GrabacionMovimiento/Crear
        // Enviar como x-www-form-urlencoded o JSON (usa Ajax).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> Crear(CrearMovimientoVm vm)
        {
            if (!ModelState.IsValid)
                return Json(new { Resultado = 0, Mensaje = "Datos inválidos." });

            try
            {
                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Movimiento_Registrar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = vm.Usuario?.Trim();
                    cmd.Parameters.Add("@IdSaldoCuenta", SqlDbType.Int).Value = vm.IdSaldoCuenta;
                    cmd.Parameters.Add("@IdTipoMovimientoCXC", SqlDbType.Int).Value = vm.IdTipoMovimientoCXC;
                    cmd.Parameters.Add("@FechaMovimiento", SqlDbType.DateTime).Value = vm.FechaMovimiento;
                    cmd.Parameters.Add("@ValorMovimiento", SqlDbType.Decimal).Value = vm.ValorMovimiento;
                    cmd.Parameters.Add("@DocumentoRef", SqlDbType.VarChar, 50).Value = (object)vm.DocumentoRef ?? DBNull.Value;
                    cmd.Parameters.Add("@Descripcion", SqlDbType.VarChar, 150).Value = (object)vm.Descripcion ?? DBNull.Value;

                    await cn.OpenAsync();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        if (!await rd.ReadAsync())
                            return Json(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "OK";
                        if (resultado != 1)
                            return Json(new { Resultado = resultado, Mensaje = mensaje });

                        object mov = null;
                        if (await rd.NextResultAsync() && await rd.ReadAsync())
                        {
                            mov = new
                            {
                                IdMovimientoCuenta = Convert.ToInt32(rd["IdMovimientoCuenta"]),
                                IdSaldoCuenta = Convert.ToInt32(rd["IdSaldoCuenta"]),
                                IdTipoMovimientoCXC = Convert.ToInt32(rd["IdTipoMovimientoCXC"]),
                                FechaMovimiento = Fmt(rd["FechaMovimiento"]),
                                ValorMovimiento = Convert.ToDecimal(rd["ValorMovimiento"]),
                                Descripcion = rd["Descripcion"] as string,
                                FechaCreacion = Fmt(rd["FechaCreacion"]),
                                UsuarioCreacion = rd["UsuarioCreacion"] as string
                            };
                        }

                        object saldo = null;
                        if (await rd.NextResultAsync() && await rd.ReadAsync())
                        {
                            saldo = new
                            {
                                IdSaldoCuenta = Convert.ToInt32(rd["IdSaldoCuenta"]),
                                SaldoAnterior = Convert.ToDecimal(rd["SaldoAnterior"]),
                                Debitos = Convert.ToDecimal(rd["Debitos"]),
                                Creditos = Convert.ToDecimal(rd["Creditos"]),
                                SaldoActual = Convert.ToDecimal(rd["SaldoActual"])
                            };
                        }

                        return Json(new { Resultado = 1, Mensaje = mensaje, Movimiento = mov, Saldo = saldo });
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { Resultado = 0, Mensaje = "Error interno: " + ex.Message });
            }
        }
    }
}
