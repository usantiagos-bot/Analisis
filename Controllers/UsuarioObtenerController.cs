﻿using System;
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
    public class UsuariosObtenerController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;

        private static string Fmt(DateTime? dt) =>
            dt.HasValue ? dt.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null;

        private IHttpActionResult Denegado(string detalle)
            => Ok(new { Resultado = 0, Mensaje = $"Permiso denegado ({detalle})." });

        [HttpGet]
        [Route("Obtener")]
        public async Task<IHttpActionResult> Obtener(
            string usuarioAccion,                   // <-- requerido para permiso
            string idUsuario = null,
            string correoElectronico = null,
            bool incluirFoto = false,
            bool incluirAuditoria = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(usuarioAccion))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar usuarioAccion." });

                if (string.IsNullOrWhiteSpace(idUsuario) && string.IsNullOrWhiteSpace(correoElectronico))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar idUsuario o correoElectronico." });

                usuarioAccion = usuarioAccion.Trim();
                idUsuario = string.IsNullOrWhiteSpace(idUsuario) ? null : idUsuario.Trim();
                correoElectronico = string.IsNullOrWhiteSpace(correoElectronico) ? null : correoElectronico.Trim();

                // Permiso de lectura: con cualquiera de estos es suficiente
                var puede =
                    await SeguridadHelper.TienePermisoAsync(usuarioAccion, Opciones.Usuarios, PermisoAccion.Imprimir) ||
                    await SeguridadHelper.TienePermisoAsync(usuarioAccion, Opciones.Usuarios, PermisoAccion.Exportar) ||
                    await SeguridadHelper.TienePermisoAsync(usuarioAccion, Opciones.Usuarios, PermisoAccion.Cambio) ||
                    await SeguridadHelper.TienePermisoAsync(usuarioAccion, Opciones.Usuarios, PermisoAccion.Alta) ||
                    await SeguridadHelper.TienePermisoAsync(usuarioAccion, Opciones.Usuarios, PermisoAccion.Baja);

                if (!puede) return Denegado("lectura");

                using (var conn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Usuario_Obtener", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdUsuario", SqlDbType.VarChar, 100).Value =
                        (object)idUsuario ?? DBNull.Value;
                    cmd.Parameters.Add("@CorreoElectronico", SqlDbType.VarChar, 100).Value =
                        (object)correoElectronico ?? DBNull.Value;
                    cmd.Parameters.Add("@IncluirFoto", SqlDbType.Bit).Value = incluirFoto;
                    cmd.Parameters.Add("@IncluirAuditoria", SqlDbType.Bit).Value = incluirAuditoria;

                    conn.Open();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        if (!rd.HasRows)
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        // RS #1: Resultado / Mensaje
                        await rd.ReadAsync();
                        int resultado = rd["Resultado"] != DBNull.Value ? Convert.ToInt32(rd["Resultado"]) : 0;
                        string mensaje = rd["Mensaje"] as string ?? "";

                        if (resultado != 1)
                            return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        // RS #2: datos del usuario
                        if (!await rd.NextResultAsync() || !await rd.ReadAsync())
                            return Ok(new { Resultado = 0, Mensaje = "No se encontraron datos del usuario." });

                        // Helpers de lectura segura
                        Func<string, string> S = name => rd[name] == DBNull.Value ? null : (string)rd[name];
                        Func<string, int?> I = name => rd[name] == DBNull.Value ? (int?)null : Convert.ToInt32(rd[name]);
                        Func<string, DateTime?> D = name => rd[name] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd[name]);

                        // Foto en base64 si viene
                        string fotoBase64 = null;
                        if (incluirFoto && rd["Fotografia"] != DBNull.Value)
                        {
                            var bytes = (byte[])rd["Fotografia"];
                            fotoBase64 = Convert.ToBase64String(bytes);
                        }

                        var data = new
                        {
                            IdUsuario = S("IdUsuario"),
                            Nombre = S("Nombre"),
                            Apellido = S("Apellido"),
                            FechaNacimiento = Fmt(D("FechaNacimiento")),
                            IdStatusUsuario = I("IdStatusUsuario"),
                            IdGenero = I("IdGenero"),
                            CorreoElectronico = S("CorreoElectronico"),
                            TelefonoMovil = S("TelefonoMovil"),
                            IdSucursal = I("IdSucursal"),
                            Pregunta = S("Pregunta"),
                            IdRole = I("IdRole"),
                            UltimaFechaIngreso = Fmt(D("UltimaFechaIngreso")),
                            IntentosDeAcceso = I("IntentosDeAcceso"),
                            SesionActual = S("SesionActual"),
                            UltimaFechaCambioPassword = Fmt(D("UltimaFechaCambioPassword")),
                            FotografiaBase64 = fotoBase64,   // solo si incluirFoto=true habrá valor

                            // Auditoría (vienen NULL si incluirAuditoria=false)
                            FechaCreacion = Fmt(D("FechaCreacion")),
                            UsuarioCreacion = S("UsuarioCreacion"),
                            FechaModificacion = Fmt(D("FechaModificacion")),
                            UsuarioModificacion = S("UsuarioModificacion")
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
    }
}
