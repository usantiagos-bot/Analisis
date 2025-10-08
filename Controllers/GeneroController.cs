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
    [RoutePrefix("Generos")]
    public class GenerosController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;

        private static string Fmt(object dt)
            => (dt == DBNull.Value || dt == null) ? null : ((DateTime)dt).ToString("yyyy-MM-ddTHH:mm:ss");

        // — Helpers para “permiso denegado”
        private IHttpActionResult Denegado(PermisoAccion acc)
            => Ok(new { Resultado = 0, Mensaje = $"Permiso denegado ({acc})." });

        private IHttpActionResult Denegado(string detalle)
            => Ok(new { Resultado = 0, Mensaje = $"Permiso denegado ({detalle})." });

        // Campos válidos para ordenar en ListarBusqueda
        private static readonly HashSet<string> CamposOrden =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "IdGenero", "Nombre", "FechaCreacion" };

        private static string NormalizarOrdenPor(string ordenPor)
            => CamposOrden.Contains(ordenPor ?? "") ? ordenPor : "Nombre";

        private static string NormalizarOrdenDir(string dir)
            => string.Equals(dir, "ASC", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

        // ========= LISTAR SIMPLE (sin filtros ni paginación) =========
        // GET /Generos/Listar?usuarioAccion=Administrador
        [HttpGet]
        [Route("Listar")]
        public async Task<IHttpActionResult> Listar(string usuarioAccion)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(usuarioAccion))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar usuarioAccion." });

                var u = usuarioAccion.Trim();
                var puede =
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Generos, PermisoAccion.Imprimir) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Generos, PermisoAccion.Exportar) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Generos, PermisoAccion.Cambio) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Generos, PermisoAccion.Alta) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Generos, PermisoAccion.Baja);

                if (!puede) return Ok(new { Resultado = 0, Mensaje = "Permiso denegado (lectura)." });

                using (var conn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Genero_Listar", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    conn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        var items = new List<object>();
                        while (await rd.ReadAsync())
                        {
                            items.Add(new
                            {
                                IdGenero = Convert.ToInt32(rd["IdGenero"]),
                                Nombre = rd["Nombre"] as string,
                                FechaCreacion = Fmt(rd["FechaCreacion"]),
                                UsuarioCreacion = rd["UsuarioCreacion"] as string,
                                FechaModificacion = Fmt(rd["FechaModificacion"]),
                                UsuarioModificacion = rd["UsuarioModificacion"] as string
                            });
                        }

                        return Ok(new { Resultado = 1, Mensaje = "OK", Items = items });
                    }
                }
            }
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error interno: " + e.Message));
            }
        }


        // ========= LISTAR CON BÚSQUEDA/PAGINACIÓN =========
        // GET /Generos/ListarBusqueda?usuarioAccion=&Buscar=&Pagina=1&TamanoPagina=50&OrdenPor=Nombre&OrdenDir=ASC
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
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Generos, PermisoAccion.Imprimir) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Generos, PermisoAccion.Exportar) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Generos, PermisoAccion.Cambio) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Generos, PermisoAccion.Alta) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Generos, PermisoAccion.Baja);

                if (!puede) return Denegado("lectura");

                OrdenPor = NormalizarOrdenPor(OrdenPor);
                OrdenDir = NormalizarOrdenDir(OrdenDir);

                using (var conn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Genero_Listar_Busqueda", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@Buscar", SqlDbType.VarChar, 100).Value =
                        (object)(string.IsNullOrWhiteSpace(Buscar) ? null : Buscar.Trim()) ?? DBNull.Value;
                    cmd.Parameters.Add("@Pagina", SqlDbType.Int).Value = Pagina;
                    cmd.Parameters.Add("@TamanoPagina", SqlDbType.Int).Value = TamanoPagina;
                    cmd.Parameters.Add("@OrdenPor", SqlDbType.VarChar, 50).Value = OrdenPor;
                    cmd.Parameters.Add("@OrdenDir", SqlDbType.VarChar, 4).Value = OrdenDir;

                    conn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        var items = new List<object>();
                        while (await rd.ReadAsync())
                        {
                            items.Add(new
                            {
                                IdGenero = rd["IdGenero"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["IdGenero"]),
                                Nombre = rd["Nombre"] as string,
                                FechaCreacion = Fmt(rd["FechaCreacion"]),
                                UsuarioCreacion = rd["UsuarioCreacion"] as string,
                                FechaModificacion = Fmt(rd["FechaModificacion"]),
                                UsuarioModificacion = rd["UsuarioModificacion"] as string
                            });
                        }

                        int total = 0;
                        if (await rd.NextResultAsync() && await rd.ReadAsync())
                            total = rd["Total"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Total"]);

                        return Ok(new
                        {
                            Resultado = 1,
                            Mensaje = "OK",
                            Pagina,
                            TamanoPagina,
                            Total = total,
                            Items = items
                        });
                    }
                }
            }
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error interno: " + e.Message));
            }
        }

        // ========= CREAR =========
        // GET /Generos/Crear?Usuario=&Nombre=
        [HttpGet]
        [Route("Crear")]
        public async Task<IHttpActionResult> Crear(string Usuario, string Nombre)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario) || string.IsNullOrWhiteSpace(Nombre))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar Usuario y Nombre." });

                var u = Usuario.Trim();
                if (!await SeguridadHelper.TienePermisoAsync(u, Opciones.Generos, PermisoAccion.Alta))
                    return Denegado(PermisoAccion.Alta);

                using (var conn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Genero_Crear", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 100).Value = Nombre.Trim();
                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = u;

                    conn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        if (!rd.HasRows)
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        await rd.ReadAsync();
                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "";

                        if (resultado != 1)
                            return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        object data = null;
                        if (await rd.NextResultAsync() && await rd.ReadAsync())
                        {
                            data = new
                            {
                                IdGenero = rd["IdGenero"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["IdGenero"]),
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
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error interno: " + e.Message));
            }
        }

        // ========= ACTUALIZAR =========
        // GET /Generos/Actualizar?Usuario=&IdGenero=&Nombre=
        [HttpGet]
        [Route("Actualizar")]
        public async Task<IHttpActionResult> Actualizar(string Usuario, int IdGenero, string Nombre = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar Usuario." });

                var u = Usuario.Trim();
                if (!await SeguridadHelper.TienePermisoAsync(u, Opciones.Generos, PermisoAccion.Cambio))
                    return Denegado(PermisoAccion.Cambio);

                using (var conn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Genero_Actualizar", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdGenero", SqlDbType.Int).Value = IdGenero;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 100).Value =
                        (object)(string.IsNullOrWhiteSpace(Nombre) ? null : Nombre.Trim()) ?? DBNull.Value;
                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = u;

                    conn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        if (!rd.HasRows)
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        await rd.ReadAsync();
                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "";

                        if (resultado != 1)
                            return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        object data = null;
                        if (await rd.NextResultAsync() && await rd.ReadAsync())
                        {
                            data = new
                            {
                                IdGenero = rd["IdGenero"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["IdGenero"]),
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
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error interno: " + e.Message));
            }
        }

        // ========= ELIMINAR =========
        // GET /Generos/Eliminar?Usuario=&IdGenero=&HardDelete=false
        [HttpGet]
        [Route("Eliminar")]
        public async Task<IHttpActionResult> Eliminar(string Usuario, int IdGenero, bool HardDelete = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar Usuario." });

                var u = Usuario.Trim();
                if (!await SeguridadHelper.TienePermisoAsync(u, Opciones.Generos, PermisoAccion.Baja))
                    return Denegado(PermisoAccion.Baja);

                using (var conn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Genero_Eliminar", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdGenero", SqlDbType.Int).Value = IdGenero;
                    cmd.Parameters.Add("@HardDelete", SqlDbType.Bit).Value = HardDelete;
                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = u;

                    conn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        if (!rd.HasRows)
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        await rd.ReadAsync();
                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "";

                        if (resultado != 1)
                            return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        // Si implementas soft-delete, aquí podrías leer un RS#2 con el registro afectado
                        return Ok(new { Resultado = 1, Mensaje = mensaje });
                    }
                }
            }
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error interno: " + e.Message));
            }
        }
    }
}
