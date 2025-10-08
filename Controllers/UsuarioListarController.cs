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
    [RoutePrefix("Usuarios")]
    public class UsuariosListarController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;

        private static string Fmt(object dt)
            => (dt == DBNull.Value || dt == null) ? null : ((DateTime)dt).ToString("yyyy-MM-ddTHH:mm:ss");

        // lista blanca para ordenamiento
        private static readonly HashSet<string> CamposOrden =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "IdUsuario", "Nombre", "Apellido", "CorreoElectronico", "FechaCreacion" };

        private static string NormalizarOrdenPor(string ordenPor)
            => CamposOrden.Contains(ordenPor ?? "") ? ordenPor : "FechaCreacion";

        private static string NormalizarOrdenDir(string dir)
            => string.Equals(dir, "ASC", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

        private IHttpActionResult Denegado(string detalle)
            => Ok(new { Resultado = 0, Mensaje = $"Permiso denegado ({detalle})." });

        [HttpGet]
        [Route("Listar")]
        public async Task<IHttpActionResult> Listar(
            string usuarioAccion,              // <-- requerido
            string buscar = null,
            int? idSucursal = null,
            int? idStatusUsuario = null,
            int? idRole = null,
            int pagina = 1,
            int tamanoPagina = 50,
            string ordenPor = "FechaCreacion", // IdUsuario|Nombre|Apellido|CorreoElectronico|FechaCreacion
            string ordenDir = "DESC"           // ASC|DESC
        )
        {
            try
            {
                if (string.IsNullOrWhiteSpace(usuarioAccion))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar usuarioAccion." });

                // Validar “lectura”: basta con que tenga cualquiera de estos permisos
                var u = usuarioAccion.Trim();
                var puede =
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Usuarios, PermisoAccion.Imprimir) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Usuarios, PermisoAccion.Exportar) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Usuarios, PermisoAccion.Cambio) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Usuarios, PermisoAccion.Alta) ||
                    await SeguridadHelper.TienePermisoAsync(u, Opciones.Usuarios, PermisoAccion.Baja);

                if (!puede) return Denegado("lectura");

                // Normaliza orden
                ordenPor = NormalizarOrdenPor(ordenPor);
                ordenDir = NormalizarOrdenDir(ordenDir);

                using (var conn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Usuario_Listar", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("@Buscar", SqlDbType.VarChar, 100).Value =
                        (object)(string.IsNullOrWhiteSpace(buscar) ? null : buscar.Trim()) ?? DBNull.Value;
                    cmd.Parameters.Add("@IdSucursal", SqlDbType.Int).Value = (object)idSucursal ?? DBNull.Value;
                    cmd.Parameters.Add("@IdStatusUsuario", SqlDbType.Int).Value = (object)idStatusUsuario ?? DBNull.Value;
                    cmd.Parameters.Add("@IdRole", SqlDbType.Int).Value = (object)idRole ?? DBNull.Value;
                    cmd.Parameters.Add("@Pagina", SqlDbType.Int).Value = pagina;
                    cmd.Parameters.Add("@TamanoPagina", SqlDbType.Int).Value = tamanoPagina;
                    cmd.Parameters.Add("@OrdenPor", SqlDbType.VarChar, 50).Value = ordenPor;
                    cmd.Parameters.Add("@OrdenDir", SqlDbType.VarChar, 4).Value = ordenDir;

                    conn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        var items = new List<object>();
                        while (rd.Read())
                        {
                            items.Add(new
                            {
                                IdUsuario = rd["IdUsuario"] as string,
                                Nombre = rd["Nombre"] as string,
                                Apellido = rd["Apellido"] as string,
                                CorreoElectronico = rd["CorreoElectronico"] as string,
                                IdSucursal = rd["IdSucursal"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["IdSucursal"]),
                                IdStatusUsuario = rd["IdStatusUsuario"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["IdStatusUsuario"]),
                                IdRole = rd["IdRole"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["IdRole"]),
                                TelefonoMovil = rd["TelefonoMovil"] as string,
                                FechaCreacion = Fmt(rd["FechaCreacion"])
                            });
                        }

                        int total = 0;
                        if (rd.NextResult() && rd.Read())
                            total = rd["Total"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Total"]);

                        return Ok(new
                        {
                            Resultado = 1,
                            Mensaje = "OK",
                            Pagina = pagina,
                            TamanoPagina = tamanoPagina,
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
    }
}
