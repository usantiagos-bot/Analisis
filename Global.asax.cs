using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Configuration;

namespace Analisis
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }


        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            var conf = (ConfigurationManager.AppSettings["AllowedOrigins"] ?? "").Trim();
            var origin = Request.Headers["Origin"];

            bool allow = false;

            if (!string.IsNullOrEmpty(origin))
            {
                if (conf == "*")
                {
                    allow = true; // todos
                }
                else
                {
                    var allowed = conf
                       .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                       .Select(s => s.Trim());

                    // match exacto o cualquier http://localhost:* / http://127.0.0.1:*
                    allow = allowed.Any(a => a.Equals(origin, StringComparison.OrdinalIgnoreCase)) ||
                            origin.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase) ||
                            origin.StartsWith("http://127.0.0.1:", StringComparison.OrdinalIgnoreCase);
                }
            }

            if (allow)
            {
                // Si usas '*', el header debe ser '*' (y sin credenciales)
                Response.Headers["Access-Control-Allow-Origin"] = conf == "*" ? "*" : origin;
                Response.Headers["Vary"] = "Origin";
                Response.Headers["Access-Control-Allow-Methods"] = "GET,POST,PUT,DELETE,OPTIONS";
                Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization, X-Requested-With";
                Response.Headers["Access-Control-Max-Age"] = "86400";
                Response.Headers["Access-Control-Allow-Credentials"] = "false";
            }

            if (Request.HttpMethod == "OPTIONS")
            {
                Response.StatusCode = 200;
                Response.End();
            }
        }

    }
}