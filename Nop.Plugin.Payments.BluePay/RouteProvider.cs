using System.Web.Mvc;
using System.Web.Routing;
using Nop.Web.Framework.Mvc.Routes;

namespace Nop.Plugin.Payments.BluePay
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(RouteCollection routes)
        {
            routes.MapRoute("Plugin.Payments.BluePay.Rebilling",
                 "Plugins/PaymentBluePay/Rebilling",
                 new { controller = "PaymentBluePay", action = "Rebilling" },
                 new[] { "Nop.Plugin.Payments.BluePay.Controllers" }
            );
        }
        public int Priority
        {
            get
            {
                return 0;
            }
        }
    }
}
