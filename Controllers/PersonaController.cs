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
    [RoutePrefix("Personas")]
    public class PersonasController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;

        private static string Fmt(object dt)
            => (dt == DBNull.Value || dt == null) ? null : ((DateTime)dt).ToString("yyyy-MM-ddTHH:mm:ss");

        private IHttpActionResult Denegado(string detalle)
            => Ok(new { Resultado = 0, Mensaje = $"Permiso denegado ({detalle})." });

        // Lista blanca para ordenamiento de ListarBusqueda
        private static readonly HashSet<string> CamposOrden =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "IdPersona", "Nombre", "Apellido", "CorreoElectronico", "FechaCreacion" };

        private static string NormalizarOrdenPor(string v) => CamposOrden.Contains(v ?? "") ? v : "FechaCreacion";
        private static string NormalizarDir(string d) => string.Equals(d, "ASC", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

        // ========= DTOs =========
        public class PersonaCrearDto
        {
            public string Usuario { get; set; }
            public string Nombre { get; set; }
            public string Apellido { get; set; }
            public DateTime? FechaNacimiento { get; set; }
            public int? IdGenero { get; set; }
            public string Direccion { get; set; }
            public string Telefono { get; set; }
            public string CorreoElectronico { get; set; }
            public int? IdEstadoCivil { get; set; }
            public string DocumentosJson { get; set; }
        }

        public class PersonaActualizarDto
        {
            public int IdPersona { get; set; }
            public string Usuario { get; set; }
            public string Nombre { get; set; }
            public string Apellido { get; set; }
            public DateTime? FechaNacimiento { get; set; }
            public int? IdGenero { get; set; }
            public string Direccion { get; set; }
            public string Telefono { get; set; }
            public string CorreoElectronico { get; set; }
            public int? IdEstadoCivil { get; set; }
            public bool ReemplazarDocumentos { get; set; } = false;
            public string DocumentosJson { get; set; }
        }

        // ========= OBTENER =========
        // GET /api/Personas/Obtener?usuarioAccion=&IdPersona=&CorreoElectronico=&IncluirDocumentos=1
        [HttpGet]
        [Route("Obtener")]
        public async Task<IHttpActionResult> Obtener(
            string usuarioAccion,
            int? IdPersona = null,
            string CorreoElectronico = null,
            bool IncluirDocumentos = true)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(usuarioAccion))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar usuarioAccion." });

                if (IdPersona == null && string.IsNullOrWhiteSpace(CorreoElectronico))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar IdPersona o CorreoElectronico." });

                var u = usuarioAccion.Trim();
                var puede =
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDePersonas, PermisoAccion.Imprimir) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDePersonas, PermisoAccion.Exportar) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDePersonas, PermisoAccion.Cambio) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDePersonas, PermisoAccion.Alta) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDePersonas, PermisoAccion.Baja);

                if (!puede) return Denegado("lectura");

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Persona_Obtener", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdPersona", SqlDbType.Int).Value = (object)IdPersona ?? DBNull.Value;
                    cmd.Parameters.Add("@CorreoElectronico", SqlDbType.VarChar, 50).Value =
                        (object)(string.IsNullOrWhiteSpace(CorreoElectronico) ? null : CorreoElectronico.Trim()) ?? DBNull.Value;
                    cmd.Parameters.Add("@IncluirDocumentos", SqlDbType.Bit).Value = IncluirDocumentos;

                    cn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        // RS#1: meta
                        if (!await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "";
                        if (resultado != 1) return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        // RS#2: persona
                        if (!await rd.NextResultAsync() || !await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "No se encontraron datos." });

                        var persona = new
                        {
                            IdPersona = Convert.ToInt32(rd["IdPersona"]),
                            Nombre = rd["Nombre"] as string,
                            Apellido = rd["Apellido"] as string,
                            FechaNacimiento = Fmt(rd["FechaNacimiento"]),
                            IdGenero = rd["IdGenero"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["IdGenero"]),
                            Direccion = rd["Direccion"] as string,
                            Telefono = rd["Telefono"] as string,
                            CorreoElectronico = rd["CorreoElectronico"] as string,
                            IdEstadoCivil = rd["IdEstadoCivil"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["IdEstadoCivil"]),
                            FechaCreacion = Fmt(rd["FechaCreacion"]),
                            UsuarioCreacion = rd["UsuarioCreacion"] as string,
                            FechaModificacion = Fmt(rd["FechaModificacion"]),
                            UsuarioModificacion = rd["UsuarioModificacion"] as string
                        };

                        // RS#3: documentos (si vienen)
                        List<object> documentos = null;
                        if (IncluirDocumentos && await rd.NextResultAsync())
                        {
                            documentos = new List<object>();
                            while (await rd.ReadAsync())
                            {
                                documentos.Add(new
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

                        return Ok(new { Resultado = 1, Mensaje = "OK", Data = persona, Documentos = documentos });
                    }
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error interno: " + ex.Message));
            }
        }

        // ========= LISTAR BUSQUEDA =========
        // GET /api/Personas/ListarBusqueda?usuarioAccion=&Buscar=&IdGenero=&IdEstadoCivil=&Pagina=1&TamanoPagina=50&OrdenPor=FechaCreacion&OrdenDir=DESC
        [HttpGet]
        [Route("ListarBusqueda")]
        public async Task<IHttpActionResult> ListarBusqueda(
            string usuarioAccion,
            string Buscar = null,
            int? IdGenero = null,
            int? IdEstadoCivil = null,
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
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDePersonas, PermisoAccion.Imprimir) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDePersonas, PermisoAccion.Exportar) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDePersonas, PermisoAccion.Cambio) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDePersonas, PermisoAccion.Alta) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDePersonas, PermisoAccion.Baja);
                if (!puede) return Denegado("lectura");

                OrdenPor = NormalizarOrdenPor(OrdenPor);
                OrdenDir = NormalizarDir(OrdenDir);

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Persona_Listar_Busqueda", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@Buscar", SqlDbType.VarChar, 100).Value =
                        (object)(string.IsNullOrWhiteSpace(Buscar) ? null : Buscar.Trim()) ?? DBNull.Value;
                    cmd.Parameters.Add("@IdGenero", SqlDbType.Int).Value = (object)IdGenero ?? DBNull.Value;
                    cmd.Parameters.Add("@IdEstadoCivil", SqlDbType.Int).Value = (object)IdEstadoCivil ?? DBNull.Value;
                    cmd.Parameters.Add("@Pagina", SqlDbType.Int).Value = Pagina;
                    cmd.Parameters.Add("@TamanoPagina", SqlDbType.Int).Value = TamanoPagina;
                    cmd.Parameters.Add("@OrdenPor", SqlDbType.VarChar, 20).Value = OrdenPor;
                    cmd.Parameters.Add("@OrdenDir", SqlDbType.VarChar, 4).Value = OrdenDir;

                    cn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        // RS#1 Meta
                        if (!await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "OK";
                        if (resultado != 1) return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        // RS#2 Items
                        if (!await rd.NextResultAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin datos." });

                        var items = new List<object>();
                        while (await rd.ReadAsync())
                        {
                            items.Add(new
                            {
                                IdPersona = Convert.ToInt32(rd["IdPersona"]),
                                Nombre = rd["Nombre"] as string,
                                Apellido = rd["Apellido"] as string,
                                CorreoElectronico = rd["CorreoElectronico"] as string,
                                Telefono = rd["Telefono"] as string,
                                FechaCreacion = Fmt(rd["FechaCreacion"]),
                                UsuarioCreacion = rd["UsuarioCreacion"] as string,
                                FechaModificacion = Fmt(rd["FechaModificacion"]),
                                UsuarioModificacion = rd["UsuarioModificacion"] as string
                            });
                        }

                        // RS#3 Total
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
        // POST /api/Personas/Crear     (Body: JSON PersonaCrearDto)
        [HttpPost]
        [Route("Crear")]
        public async Task<IHttpActionResult> Crear([FromBody] PersonaCrearDto model)
        {
            try
            {
                if (model == null ||
                    string.IsNullOrWhiteSpace(model.Usuario) ||
                    string.IsNullOrWhiteSpace(model.Nombre) ||
                    string.IsNullOrWhiteSpace(model.Apellido))
                {
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar Usuario, Nombre y Apellido." });
                }

                var u = model.Usuario.Trim();
                if (!await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDePersonas, PermisoAccion.Alta))
                    return Denegado(PermisoAccion.Alta.ToString());

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Persona_Crear", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 50).Value = model.Nombre.Trim();
                    cmd.Parameters.Add("@Apellido", SqlDbType.VarChar, 50).Value = model.Apellido.Trim();
                    cmd.Parameters.Add("@FechaNacimiento", SqlDbType.Date).Value = (object)model.FechaNacimiento ?? DBNull.Value;
                    cmd.Parameters.Add("@IdGenero", SqlDbType.Int).Value = (object)model.IdGenero ?? DBNull.Value;
                    cmd.Parameters.Add("@Direccion", SqlDbType.VarChar, 100).Value = (object)model.Direccion ?? DBNull.Value;
                    cmd.Parameters.Add("@Telefono", SqlDbType.VarChar, 50).Value = (object)model.Telefono ?? DBNull.Value;
                    cmd.Parameters.Add("@CorreoElectronico", SqlDbType.VarChar, 50).Value = (object)model.CorreoElectronico ?? DBNull.Value;
                    cmd.Parameters.Add("@IdEstadoCivil", SqlDbType.Int).Value = (object)model.IdEstadoCivil ?? DBNull.Value;
                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = u;
                    cmd.Parameters.Add("@DocumentosJson", SqlDbType.NVarChar, -1).Value =
                        (object)(string.IsNullOrWhiteSpace(model.DocumentosJson) ? null : model.DocumentosJson) ?? DBNull.Value;

                    cn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        if (!await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "";
                        if (resultado != 1) return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        object persona = null;
                        if (await rd.NextResultAsync() && await rd.ReadAsync())
                        {
                            persona = new
                            {
                                IdPersona = Convert.ToInt32(rd["IdPersona"]),
                                Nombre = rd["Nombre"] as string,
                                Apellido = rd["Apellido"] as string,
                                CorreoElectronico = rd["CorreoElectronico"] as string,
                                FechaCreacion = Fmt(rd["FechaCreacion"]),
                                UsuarioCreacion = rd["UsuarioCreacion"] as string
                            };
                        }

                        return Ok(new { Resultado = 1, Mensaje = mensaje, Data = persona });
                    }
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error interno: " + ex.Message));
            }
        }

        // ========= ACTUALIZAR =========
        // POST /api/Personas/Actualizar     (Body: JSON PersonaActualizarDto)
        [HttpPost]
        [Route("Actualizar")]
        public async Task<IHttpActionResult> Actualizar([FromBody] PersonaActualizarDto model)
        {
            try
            {
                if (model == null || string.IsNullOrWhiteSpace(model.Usuario))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar Usuario." });

                var u = model.Usuario.Trim();
                if (!await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDePersonas, PermisoAccion.Cambio))
                    return Denegado(PermisoAccion.Cambio.ToString());

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Persona_Actualizar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdPersona", SqlDbType.Int).Value = model.IdPersona;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 50).Value = (object)model.Nombre ?? DBNull.Value;
                    cmd.Parameters.Add("@Apellido", SqlDbType.VarChar, 50).Value = (object)model.Apellido ?? DBNull.Value;
                    cmd.Parameters.Add("@FechaNacimiento", SqlDbType.Date).Value = (object)model.FechaNacimiento ?? DBNull.Value;
                    cmd.Parameters.Add("@IdGenero", SqlDbType.Int).Value = (object)model.IdGenero ?? DBNull.Value;
                    cmd.Parameters.Add("@Direccion", SqlDbType.VarChar, 100).Value = (object)model.Direccion ?? DBNull.Value;
                    cmd.Parameters.Add("@Telefono", SqlDbType.VarChar, 50).Value = (object)model.Telefono ?? DBNull.Value;
                    cmd.Parameters.Add("@CorreoElectronico", SqlDbType.VarChar, 50).Value = (object)model.CorreoElectronico ?? DBNull.Value;
                    cmd.Parameters.Add("@IdEstadoCivil", SqlDbType.Int).Value = (object)model.IdEstadoCivil ?? DBNull.Value;
                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = u;

                    cmd.Parameters.Add("@ReemplazarDocumentos", SqlDbType.Bit).Value = model.ReemplazarDocumentos;
                    cmd.Parameters.Add("@DocumentosJson", SqlDbType.NVarChar, -1).Value =
                        (object)(string.IsNullOrWhiteSpace(model.DocumentosJson) ? null : model.DocumentosJson) ?? DBNull.Value;

                    cn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        if (!await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "";
                        if (resultado != 1) return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        object persona = null;
                        if (await rd.NextResultAsync() && await rd.ReadAsync())
                        {
                            persona = new
                            {
                                IdPersona = Convert.ToInt32(rd["IdPersona"]),
                                Nombre = rd["Nombre"] as string,
                                Apellido = rd["Apellido"] as string,
                                CorreoElectronico = rd["CorreoElectronico"] as string,
                                FechaCreacion = Fmt(rd["FechaCreacion"]),
                                UsuarioCreacion = rd["UsuarioCreacion"] as string,
                                FechaModificacion = Fmt(rd["FechaModificacion"]),
                                UsuarioModificacion = rd["UsuarioModificacion"] as string
                            };
                        }

                        List<object> documentos = null;
                        if (model.ReemplazarDocumentos && await rd.NextResultAsync())
                        {
                            documentos = new List<object>();
                            while (await rd.ReadAsync())
                            {
                                documentos.Add(new
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

                        return Ok(new { Resultado = 1, Mensaje = mensaje, Data = persona, Documentos = documentos });
                    }
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error interno: " + ex.Message));
            }
        }

        // ========= ELIMINAR =========
        // DELETE /api/Personas/Eliminar?Usuario=&IdPersona=
        [HttpDelete]
        [Route("Eliminar")]
        public async Task<IHttpActionResult> Eliminar(string Usuario, int IdPersona)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Usuario))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar Usuario." });

                var u = Usuario.Trim();
                if (!await SeguridadHelper.TienePermisoAsync(u, Opciones.GestionDePersonas, PermisoAccion.Baja))
                    return Denegado(PermisoAccion.Baja.ToString());

                using (var cn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Persona_Eliminar", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdPersona", SqlDbType.Int).Value = IdPersona;

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
