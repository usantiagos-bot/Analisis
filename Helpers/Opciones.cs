using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ProyectoAnalisis.Permissions
{
    public static class Opciones
    {
        public const int Empresas = 1;
        public const int Sucursales = 2;
        public const int Generos = 3;
        public const int EstatusUsuario = 4;
        public const int Roles = 5;
        public const int Modulos = 6;
        public const int Menus = 7;
        public const int CatalogoOpciones = 8; // <- antes: Opciones (RENOMBRADA)
        public const int Usuarios = 9;

        // (Opcional) Otros IdOpcion que se ven en tu tabla:
        public const int AsignarOpcionesARol = 10;
        public const int StatusDeCuentas = 11;
        public const int EstadoCivilDePersonas = 12;
        public const int TiposDeDocumentos = 13;
        public const int TiposMovimientoCxc = 14;
        public const int TiposDeCuentas = 15;
        public const int GestionDePersonas = 16;
        public const int GestionDeCuentas = 17;
        public const int ConsultaDeSaldos = 18;
        public const int EstadoDeCuentas = 19;
        public const int GrabacionDeMovimientos = 20;
        public const int CierreDeMes = 21;
    }
}
