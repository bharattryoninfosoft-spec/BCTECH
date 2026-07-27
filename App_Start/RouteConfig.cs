using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace BCTECHERP
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new
                {
                    controller = "bctechadmin",
                    action = "Index",
                    id = UrlParameter.Optional
                }
            );
            routes.MapRoute(
                name: "UserEdit",
                url: "bctechadmin/CreateUser/{id}/{mode}",
                defaults: new
                {
                    controller = "bctechadmin",
                    action = "CreateUser",
                    id = UrlParameter.Optional,
                    mode = UrlParameter.Optional
                }
            );
        }
    }
}
