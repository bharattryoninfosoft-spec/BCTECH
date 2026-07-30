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
                    controller = "bctechadmin",   // Your controller name
                    action = "Index",             // Default action
                    id = UrlParameter.Optional
                }
            );
        }
    }
}