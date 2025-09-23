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
    [RoutePrefix("Opciones")]
    public class OpcionesController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;

        private static string Fmt(object dt)
            => (dt == DBNull.Value || dt == null) ? null : ((DateTime)dt).ToString("yyyy-MM-ddTHH:mm:ss");

        // —— Helpers “permiso denegado”
        private IHttpActionResult Denegado(PermisoAccion acc)
            => Ok(new { Resultado = 0, Mensaje = $"Permiso denegado ({acc})." });

        private IHttpActionResult Denegado(string detalle)
            => Ok(new { Resultado = 0, Mensaje = $"Permiso denegado ({detalle})." });

        // Campos válidos para ordenar en Listar/ListarBusqueda
        private static readonly HashSet<string> CamposOrden =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "IdOpcion", "IdMenu", "Nombre", "Pagina", "OrdenMenu", "FechaCreacion" };

        private static string NormalizarOrdenPor(string ordenPor)
            => CamposOrden.Contains(ordenPor ?? "") ? ordenPor : "Nombre";

        private static string NormalizarOrdenDir(string dir)
            => string.Equals(dir, "ASC", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

        // ========= LISTAR (un solo registro por filtros) =========
        // GET /Opciones/Listar?usuarioAccion=&IdOpcion=&IdMenu=&Nombre=&Pagina=&incluirAuditoria=false
        [HttpGet]
        [Route("Listar")]
        public async Task<IHttpActionResult> Listar(
            string usuarioAccion,
            int? IdOpcion = null,
            int? IdMenu = null,
            string Nombre = null,
            string Pagina = null,
            bool incluirAuditoria = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(usuarioAccion))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar usuarioAccion." });

                if (IdOpcion == null && IdMenu == null && string.IsNullOrWhiteSpace(Nombre) && string.IsNullOrWhiteSpace(Pagina))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar al menos un filtro (IdOpcion, IdMenu, Nombre o Pagina)." });

                var u = usuarioAccion.Trim();
                var puede =
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.CatalogoOpciones, PermisoAccion.Imprimir) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.CatalogoOpciones, PermisoAccion.Exportar) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.CatalogoOpciones, PermisoAccion.Cambio) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.CatalogoOpciones, PermisoAccion.Alta) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.CatalogoOpciones, PermisoAccion.Baja);

                if (!puede) return Denegado("lectura");

                using (var conn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Opcion_Listar", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdOpcion", SqlDbType.Int).Value = (object)IdOpcion ?? DBNull.Value;
                    cmd.Parameters.Add("@IdMenu", SqlDbType.Int).Value = (object)IdMenu ?? DBNull.Value;
                    cmd.Parameters.Add("@BuscarNombre", SqlDbType.VarChar, 100).Value =
                        (object)(string.IsNullOrWhiteSpace(Nombre) ? null : Nombre.Trim()) ?? DBNull.Value;
                    cmd.Parameters.Add("@BuscarPagina", SqlDbType.VarChar, 100).Value =
                        (object)(string.IsNullOrWhiteSpace(Pagina) ? null : Pagina.Trim()) ?? DBNull.Value;
                    cmd.Parameters.Add("@Page", SqlDbType.Int).Value = 1;
                    cmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = 1;
                    cmd.Parameters.Add("@OrdenPor", SqlDbType.VarChar, 20).Value = "Nombre";
                    cmd.Parameters.Add("@OrdenDir", SqlDbType.VarChar, 4).Value = "ASC";

                    conn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        if (!rd.Read())
                            return Ok(new { Resultado = 0, Mensaje = "No se encontraron datos." });

                        var data = new
                        {
                            IdOpcion = Convert.ToInt32(rd["IdOpcion"]),
                            IdMenu = rd["IdMenu"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["IdMenu"]),
                            Nombre = rd["Nombre"] as string,
                            Pagina = rd["Pagina"] as string,
                            OrdenMenu = rd["OrdenMenu"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["OrdenMenu"]),
                            FechaCreacion = Fmt(rd["FechaCreacion"]),
                            UsuarioCreacion = incluirAuditoria ? rd["UsuarioCreacion"] as string : null,
                            FechaModificacion = incluirAuditoria ? Fmt(rd["FechaModificacion"]) : null,
                            UsuarioModificacion = incluirAuditoria ? rd["UsuarioModificacion"] as string : null
                        };

                        return Ok(new { Resultado = 1, Mensaje = "OK", Data = data });
                    }
                }
            }
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error interno: " + e.Message));
            }
        }

        // ========= LISTAR CON BÚSQUEDA/PAGINACIÓN =========
        // GET /Opciones/ListarBusqueda?usuarioAccion=&Buscar=&IdMenu=&Pagina=1&TamanoPagina=50&OrdenPor=Nombre&OrdenDir=ASC
        [HttpGet]
        [Route("ListarBusqueda")]
        public async Task<IHttpActionResult> ListarBusqueda(
            string usuarioAccion,
            string Buscar = null,
            int? IdMenu = null,
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
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.CatalogoOpciones, PermisoAccion.Imprimir) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.CatalogoOpciones, PermisoAccion.Exportar) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.CatalogoOpciones, PermisoAccion.Cambio) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.CatalogoOpciones, PermisoAccion.Alta) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.CatalogoOpciones, PermisoAccion.Baja);

                if (!puede) return Denegado("lectura");

                OrdenPor = NormalizarOrdenPor(OrdenPor);
                OrdenDir = NormalizarOrdenDir(OrdenDir);

                using (var conn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Opcion_Listar_Busqueda", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@Buscar", SqlDbType.VarChar, 100).Value =
                        (object)(string.IsNullOrWhiteSpace(Buscar) ? null : Buscar.Trim()) ?? DBNull.Value;
                    cmd.Parameters.Add("@IdMenu", SqlDbType.Int).Value = (object)IdMenu ?? DBNull.Value;
                    cmd.Parameters.Add("@Page", SqlDbType.Int).Value = Pagina;
                    cmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = TamanoPagina;
                    cmd.Parameters.Add("@OrdenPor", SqlDbType.VarChar, 20).Value = OrdenPor;
                    cmd.Parameters.Add("@OrdenDir", SqlDbType.VarChar, 4).Value = OrdenDir;

                    conn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        var items = new List<object>();
                        while (await rd.ReadAsync())
                        {
                            items.Add(new
                            {
                                IdOpcion = Convert.ToInt32(rd["IdOpcion"]),
                                IdMenu = rd["IdMenu"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["IdMenu"]),
                                Nombre = rd["Nombre"] as string,
                                Pagina = rd["Pagina"] as string,
                                OrdenMenu = rd["OrdenMenu"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["OrdenMenu"]),
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
        // GET /Opciones/Crear?Usuario=&IdMenu=&Nombre=&Pagina=&OrdenMenu=
        [HttpGet]
        [Route("Crear")]
        public async Task<IHttpActionResult> Crear(string Usuario, int IdMenu, string Nombre, string Pagina, int OrdenMenu)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario) ||
                    string.IsNullOrWhiteSpace(Nombre) ||
                    string.IsNullOrWhiteSpace(Pagina))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar Usuario, IdMenu, Nombre, Pagina y OrdenMenu." });

                var u = Usuario.Trim();
                if (!await SeguridadHelper.TienePermisoAsync(u, Opciones.CatalogoOpciones, PermisoAccion.Alta))
                    return Denegado(PermisoAccion.Alta);

                using (var conn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Opcion_Crear", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdMenu", SqlDbType.Int).Value = IdMenu;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 50).Value = Nombre.Trim();
                    cmd.Parameters.Add("@Pagina", SqlDbType.VarChar, 100).Value = Pagina.Trim();
                    cmd.Parameters.Add("@OrdenMenu", SqlDbType.Int).Value = OrdenMenu;
                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = u;

                    conn.Open();
                    var scalar = await cmd.ExecuteScalarAsync();
                    var id = Convert.ToInt32(scalar);

                    return Ok(new { Resultado = 1, Mensaje = "Creado", IdOpcion = id });
                }
            }
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error interno: " + e.Message));
            }
        }

        // ========= ACTUALIZAR =========
        // GET /Opciones/Actualizar?Usuario=&IdOpcion=&IdMenu=&Nombre=&Pagina=&OrdenMenu=
        [HttpGet]
        [Route("Actualizar")]
        public async Task<IHttpActionResult> Actualizar(
            string Usuario,
            int IdOpcion,
            int? IdMenu = null,
            string Nombre = null,
            string Pagina = null,
            int? OrdenMenu = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar Usuario." });

                var u = Usuario.Trim();
                if (!await SeguridadHelper.TienePermisoAsync(u, Opciones.CatalogoOpciones, PermisoAccion.Cambio))
                    return Denegado(PermisoAccion.Cambio);

                using (var conn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Opcion_Actualizar", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdOpcion", SqlDbType.Int).Value = IdOpcion;
                    cmd.Parameters.Add("@IdMenu", SqlDbType.Int).Value = (object)IdMenu ?? DBNull.Value;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 50).Value =
                        (object)(string.IsNullOrWhiteSpace(Nombre) ? null : Nombre.Trim()) ?? DBNull.Value;
                    cmd.Parameters.Add("@Pagina", SqlDbType.VarChar, 100).Value =
                        (object)(string.IsNullOrWhiteSpace(Pagina) ? null : Pagina.Trim()) ?? DBNull.Value;
                    cmd.Parameters.Add("@OrdenMenu", SqlDbType.Int).Value = (object)OrdenMenu ?? DBNull.Value;
                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = u;

                    conn.Open();
                    await cmd.ExecuteNonQueryAsync();

                    return Ok(new { Resultado = 1, Mensaje = "Actualizado" });
                }
            }
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error interno: " + e.Message));
            }
        }

        // ========= ELIMINAR =========
        // GET /Opciones/Eliminar?Usuario=&IdOpcion=
        [HttpGet]
        [Route("Eliminar")]
        public async Task<IHttpActionResult> Eliminar(string Usuario, int IdOpcion)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar Usuario." });

                var u = Usuario.Trim();
                if (!await SeguridadHelper.TienePermisoAsync(u, Opciones.CatalogoOpciones, PermisoAccion.Baja))
                    return Denegado(PermisoAccion.Baja);

                using (var conn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Opcion_Eliminar", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdOpcion", SqlDbType.Int).Value = IdOpcion;

                    conn.Open();
                    await cmd.ExecuteNonQueryAsync();

                    return Ok(new { Resultado = 1, Mensaje = "Eliminado" });
                }
            }
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error interno: " + e.Message));
            }
        }
    }
}
