using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;

namespace TuProyecto.Controllers
{
    public class RoleOpcionController : Controller
    {
        // ====== DTOs ======
        public class RoleOpcionDto
        {
            public int IdRole { get; set; }
            public int IdOpcion { get; set; }
            public int Alta { get; set; }
            public int Baja { get; set; }
            public int Cambio { get; set; }
            public int Imprimir { get; set; }
            public int Exportar { get; set; }

            // Fechas como string con formato requerido
            public string FechaCreacion { get; set; }     // yyyy-MM-ddTHH:mm:ss
            public string UsuarioCreacion { get; set; }
            public string FechaModificacion { get; set; } // yyyy-MM-ddTHH:mm:ss
            public string UsuarioModificacion { get; set; }
        }

        public class Paginado<T>
        {
            public int Resultado { get; set; }
            public string Mensaje { get; set; }
            public int Pagina { get; set; }
            public int TamanoPagina { get; set; }
            public int Total { get; set; }
            public List<T> Items { get; set; }
        }

        private static string ConnStr =>
            ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;

        private static string Fmt(DateTime? dt)
            => dt.HasValue ? dt.Value.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) : null;

        // ====== LISTAR (paginado) ======
        // GET: /RoleOpcion/Listar?IdRole=&IdOpcion=&Pagina=1&TamanoPagina=20&OrdenPor=IdRole&OrdenDir=ASC
        [HttpGet]
        public JsonResult Listar(
            int? IdRole = null,
            int? IdOpcion = null,
            int Pagina = 1,
            int TamanoPagina = 20,
            string OrdenPor = "IdRole",
            string OrdenDir = "ASC")
        {
            var result = new Paginado<RoleOpcionDto>
            {
                Resultado = 1,
                Mensaje = "OK",
                Pagina = Pagina < 1 ? 1 : Pagina,
                TamanoPagina = TamanoPagina < 1 ? 20 : TamanoPagina,
                Total = 0,
                Items = new List<RoleOpcionDto>()
            };

            try
            {
                using (var cn = new SqlConnection(ConnStr))
                using (var cmd = new SqlCommand("dbo.sp_RoleOpcion_Listar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IdRole", (object)IdRole ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@IdOpcion", (object)IdOpcion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Pagina", result.Pagina);
                    cmd.Parameters.AddWithValue("@TamanoPagina", result.TamanoPagina);
                    cmd.Parameters.AddWithValue("@OrdenPor", (object)OrdenPor ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@OrdenDir", (object)OrdenDir ?? DBNull.Value);

                    cn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        // 1er result set: Total
                        if (rd.Read())
                        {
                            result.Total = rd["Total"] != DBNull.Value ? Convert.ToInt32(rd["Total"]) : 0;
                        }

                        // 2do result set: Items
                        if (rd.NextResult())
                        {
                            while (rd.Read())
                            {
                                var dto = new RoleOpcionDto
                                {
                                    IdRole = Convert.ToInt32(rd["IdRole"]),
                                    IdOpcion = Convert.ToInt32(rd["IdOpcion"]),
                                    Alta = Convert.ToInt32(rd["Alta"]),
                                    Baja = Convert.ToInt32(rd["Baja"]),
                                    Cambio = Convert.ToInt32(rd["Cambio"]),
                                    Imprimir = Convert.ToInt32(rd["Imprimir"]),
                                    Exportar = Convert.ToInt32(rd["Exportar"]),
                                    FechaCreacion = Fmt(rd["FechaCreacion"] as DateTime? ?? (rd["FechaCreacion"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["FechaCreacion"]))),
                                    UsuarioCreacion = rd["UsuarioCreacion"] as string,
                                    FechaModificacion = Fmt(rd["FechaModificacion"] as DateTime? ?? (rd["FechaModificacion"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["FechaModificacion"]))),
                                    UsuarioModificacion = rd["UsuarioModificacion"] as string
                                };
                                result.Items.Add(dto);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Resultado = 0;
                result.Mensaje = ex.Message;
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        // ====== OBTENER (por PK) ======
        // GET: /RoleOpcion/Obtener?IdRole=1&IdOpcion=2
        [HttpGet]
        public JsonResult Obtener(int IdRole, int IdOpcion)
        {
            var resp = new
            {
                Resultado = 1,
                Mensaje = "OK",
                Data = (RoleOpcionDto)null
            };

            try
            {
                RoleOpcionDto data = null;

                using (var cn = new SqlConnection(ConnStr))
                using (var cmd = new SqlCommand("dbo.sp_RoleOpcion_Obtener", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IdRole", IdRole);
                    cmd.Parameters.AddWithValue("@IdOpcion", IdOpcion);

                    cn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        // 1er result set: Resultado/Mensaje (opcional)
                        if (rd.Read())
                        {
                            // Si tu SP devuelve Resultado/Mensaje en el 1er set, puedes leerlos aquí.
                            // Este controlador ignora y compone su propio contrato.
                        }

                        // 2do result set: Data
                        if (rd.NextResult() && rd.Read())
                        {
                            data = new RoleOpcionDto
                            {
                                IdRole = Convert.ToInt32(rd["IdRole"]),
                                IdOpcion = Convert.ToInt32(rd["IdOpcion"]),
                                Alta = Convert.ToInt32(rd["Alta"]),
                                Baja = Convert.ToInt32(rd["Baja"]),
                                Cambio = Convert.ToInt32(rd["Cambio"]),
                                Imprimir = Convert.ToInt32(rd["Imprimir"]),
                                Exportar = Convert.ToInt32(rd["Exportar"]),
                                FechaCreacion = Fmt(rd["FechaCreacion"] as DateTime? ?? (rd["FechaCreacion"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["FechaCreacion"]))),
                                UsuarioCreacion = rd["UsuarioCreacion"] as string,
                                FechaModificacion = Fmt(rd["FechaModificacion"] as DateTime? ?? (rd["FechaModificacion"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["FechaModificacion"]))),
                                UsuarioModificacion = rd["UsuarioModificacion"] as string
                            };
                        }
                    }
                }

                return Json(new { Resultado = data != null ? 1 : 0, Mensaje = data != null ? "OK" : "No existe el registro", Data = data }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Resultado = 0, Mensaje = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // ====== CREAR ======
        // GET: /RoleOpcion/Crear?IdRole=1&IdOpcion=2&Alta=1&Baja=0&Cambio=1&Imprimir=1&Exportar=0&UsuarioAccion=admin
        [HttpGet]
        public JsonResult Crear(int IdRole, int IdOpcion, int Alta, int Baja, int Cambio, int Imprimir, int Exportar, string UsuarioAccion)
        {
            try
            {
                using (var cn = new SqlConnection(ConnStr))
                using (var cmd = new SqlCommand("dbo.sp_RoleOpcion_Crear", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IdRole", IdRole);
                    cmd.Parameters.AddWithValue("@IdOpcion", IdOpcion);
                    cmd.Parameters.AddWithValue("@Alta", Alta);
                    cmd.Parameters.AddWithValue("@Baja", Baja);
                    cmd.Parameters.AddWithValue("@Cambio", Cambio);
                    cmd.Parameters.AddWithValue("@Imprimir", Imprimir);
                    cmd.Parameters.AddWithValue("@Exportar", Exportar);
                    cmd.Parameters.AddWithValue("@UsuarioAccion", UsuarioAccion ?? (object)DBNull.Value);

                    cn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        int resultado = 1; string mensaje = "Creado correctamente";
                        // 1er set: Resultado/Mensaje del SP
                        if (rd.Read())
                        {
                            resultado = rd["Resultado"] != DBNull.Value ? Convert.ToInt32(rd["Resultado"]) : 0;
                            mensaje = rd["Mensaje"] as string ?? "";
                        }

                        RoleOpcionDto data = null;
                        if (rd.NextResult() && rd.Read())
                        {
                            data = new RoleOpcionDto
                            {
                                IdRole = Convert.ToInt32(rd["IdRole"]),
                                IdOpcion = Convert.ToInt32(rd["IdOpcion"]),
                                Alta = Convert.ToInt32(rd["Alta"]),
                                Baja = Convert.ToInt32(rd["Baja"]),
                                Cambio = Convert.ToInt32(rd["Cambio"]),
                                Imprimir = Convert.ToInt32(rd["Imprimir"]),
                                Exportar = Convert.ToInt32(rd["Exportar"]),
                                FechaCreacion = Fmt(rd["FechaCreacion"] as DateTime? ?? (rd["FechaCreacion"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["FechaCreacion"]))),
                                UsuarioCreacion = rd["UsuarioCreacion"] as string
                            };
                        }

                        return Json(new { Resultado = resultado, Mensaje = mensaje, Data = data }, JsonRequestBehavior.AllowGet);
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { Resultado = 0, Mensaje = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // ====== ACTUALIZAR ======
        // GET: /RoleOpcion/Actualizar?... (mismos parámetros que Crear)
        [HttpGet]
        public JsonResult Actualizar(int IdRole, int IdOpcion, int Alta, int Baja, int Cambio, int Imprimir, int Exportar, string UsuarioAccion)
        {
            try
            {
                using (var cn = new SqlConnection(ConnStr))
                using (var cmd = new SqlCommand("dbo.sp_RoleOpcion_Actualizar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IdRole", IdRole);
                    cmd.Parameters.AddWithValue("@IdOpcion", IdOpcion);
                    cmd.Parameters.AddWithValue("@Alta", Alta);
                    cmd.Parameters.AddWithValue("@Baja", Baja);
                    cmd.Parameters.AddWithValue("@Cambio", Cambio);
                    cmd.Parameters.AddWithValue("@Imprimir", Imprimir);
                    cmd.Parameters.AddWithValue("@Exportar", Exportar);
                    cmd.Parameters.AddWithValue("@UsuarioAccion", UsuarioAccion ?? (object)DBNull.Value);

                    cn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        int resultado = 1; string mensaje = "Actualizado correctamente";
                        if (rd.Read())
                        {
                            resultado = rd["Resultado"] != DBNull.Value ? Convert.ToInt32(rd["Resultado"]) : 0;
                            mensaje = rd["Mensaje"] as string ?? "";
                        }

                        RoleOpcionDto data = null;
                        if (rd.NextResult() && rd.Read())
                        {
                            data = new RoleOpcionDto
                            {
                                IdRole = Convert.ToInt32(rd["IdRole"]),
                                IdOpcion = Convert.ToInt32(rd["IdOpcion"]),
                                Alta = Convert.ToInt32(rd["Alta"]),
                                Baja = Convert.ToInt32(rd["Baja"]),
                                Cambio = Convert.ToInt32(rd["Cambio"]),
                                Imprimir = Convert.ToInt32(rd["Imprimir"]),
                                Exportar = Convert.ToInt32(rd["Exportar"]),
                                FechaModificacion = Fmt(rd["FechaModificacion"] as DateTime? ?? (rd["FechaModificacion"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["FechaModificacion"]))),
                                UsuarioModificacion = rd["UsuarioModificacion"] as string
                            };
                        }

                        return Json(new { Resultado = resultado, Mensaje = mensaje, Data = data }, JsonRequestBehavior.AllowGet);
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { Resultado = 0, Mensaje = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // ====== GUARDAR (Upsert) ======
        // GET: /RoleOpcion/Guardar?... (mismos parámetros que Crear)
        [HttpGet]
        public JsonResult Guardar(int IdRole, int IdOpcion, int Alta, int Baja, int Cambio, int Imprimir, int Exportar, string UsuarioAccion)
        {
            try
            {
                using (var cn = new SqlConnection(ConnStr))
                using (var cmd = new SqlCommand("dbo.sp_RoleOpcion_Guardar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IdRole", IdRole);
                    cmd.Parameters.AddWithValue("@IdOpcion", IdOpcion);
                    cmd.Parameters.AddWithValue("@Alta", Alta);
                    cmd.Parameters.AddWithValue("@Baja", Baja);
                    cmd.Parameters.AddWithValue("@Cambio", Cambio);
                    cmd.Parameters.AddWithValue("@Imprimir", Imprimir);
                    cmd.Parameters.AddWithValue("@Exportar", Exportar);
                    cmd.Parameters.AddWithValue("@UsuarioAccion", UsuarioAccion ?? (object)DBNull.Value);

                    cn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        int resultado = 1; string mensaje = "OK";
                        if (rd.Read())
                        {
                            resultado = rd["Resultado"] != DBNull.Value ? Convert.ToInt32(rd["Resultado"]) : 0;
                            mensaje = rd["Mensaje"] as string ?? "";
                        }

                        RoleOpcionDto data = null;
                        if (rd.NextResult() && rd.Read())
                        {
                            data = new RoleOpcionDto
                            {
                                IdRole = Convert.ToInt32(rd["IdRole"]),
                                IdOpcion = Convert.ToInt32(rd["IdOpcion"]),
                                Alta = Convert.ToInt32(rd["Alta"]),
                                Baja = Convert.ToInt32(rd["Baja"]),
                                Cambio = Convert.ToInt32(rd["Cambio"]),
                                Imprimir = Convert.ToInt32(rd["Imprimir"]),
                                Exportar = Convert.ToInt32(rd["Exportar"]),
                                FechaCreacion = Fmt(rd["FechaCreacion"] as DateTime? ?? (rd["FechaCreacion"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["FechaCreacion"]))),
                                UsuarioCreacion = rd["UsuarioCreacion"] as string,
                                FechaModificacion = Fmt(rd["FechaModificacion"] as DateTime? ?? (rd["FechaModificacion"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["FechaModificacion"]))),
                                UsuarioModificacion = rd["UsuarioModificacion"] as string
                            };
                        }

                        return Json(new { Resultado = resultado, Mensaje = mensaje, Data = data }, JsonRequestBehavior.AllowGet);
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { Resultado = 0, Mensaje = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // ====== ELIMINAR ======
        // GET: /RoleOpcion/Eliminar?IdRole=1&IdOpcion=2
        [HttpGet]
        public JsonResult Eliminar(int IdRole, int IdOpcion)
        {
            try
            {
                using (var cn = new SqlConnection(ConnStr))
                using (var cmd = new SqlCommand("dbo.sp_RoleOpcion_Eliminar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IdRole", IdRole);
                    cmd.Parameters.AddWithValue("@IdOpcion", IdOpcion);

                    cn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        int resultado = 1; string mensaje = "Eliminado correctamente";
                        if (rd.Read())
                        {
                            resultado = rd["Resultado"] != DBNull.Value ? Convert.ToInt32(rd["Resultado"]) : 0;
                            mensaje = rd["Mensaje"] as string ?? "";
                        }

                        return Json(new { Resultado = resultado, Mensaje = mensaje }, JsonRequestBehavior.AllowGet);
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { Resultado = 0, Mensaje = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}
