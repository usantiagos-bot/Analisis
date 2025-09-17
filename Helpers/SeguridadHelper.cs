using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace ProyectoAnalisis.Helpers
{
    // Evita errores por typos y te da IntelliSense
    public enum PermisoAccion { Alta, Baja, Cambio, Imprimir, Exportar }

    public static class SeguridadHelper
    {
        private static string Cnx =>
            ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;

        // Validar por IdOpcion (principal)
        public static async Task<bool> TienePermisoAsync(string idUsuario, int idOpcion, PermisoAccion permiso)
        {
            if (string.IsNullOrWhiteSpace(idUsuario)) return false;

            using (var cn = new SqlConnection(Cnx))
            using (var cmd = new SqlCommand("dbo.sp_Seguridad_ValidarPermiso", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@IdUsuario", SqlDbType.VarChar, 100).Value = idUsuario.Trim();
                cmd.Parameters.Add("@IdOpcion", SqlDbType.Int).Value = idOpcion;
                cmd.Parameters.Add("@Permiso", SqlDbType.VarChar, 10).Value = permiso.ToString();

                await cn.OpenAsync().ConfigureAwait(false);
                var obj = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                return obj != null && Convert.ToInt32(obj) == 1;
            }
        }

        // Overload por string (por si algún sitio te llega como texto)
        public static Task<bool> TienePermisoAsync(string idUsuario, int idOpcion, string permiso)
        {
            var map = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Alta","Baja","Cambio","Imprimir","Exportar" };
            if (!map.Contains(permiso ?? "")) return Task.FromResult(false);

            var p = (PermisoAccion)Enum.Parse(typeof(PermisoAccion), permiso, true);
            return TienePermisoAsync(idUsuario, idOpcion, p);
        }
    }
}
