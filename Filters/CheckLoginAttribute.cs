using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace BCTECHERP.Filters
{
    public class CheckLoginAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            HttpCookie cookie = HttpContext.Current.Request.Cookies["UserID"];

            if (cookie == null || string.IsNullOrEmpty(cookie.Value))
            {
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary
                    {
                        { "controller", "bctechadmin" },
                        { "action", "Index" }
                    });

                return;
            }

            HttpContext.Current.Response.Cache.SetExpires(DateTime.UtcNow.AddMinutes(-1));
            HttpContext.Current.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            HttpContext.Current.Response.Cache.SetNoStore();
            HttpContext.Current.Response.Cache.SetRevalidation(HttpCacheRevalidation.AllCaches);

            base.OnActionExecuting(filterContext);
        }
    }
}