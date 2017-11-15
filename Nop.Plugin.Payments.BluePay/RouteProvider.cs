using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.BluePay
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(IRouteBuilder routeBuilder)
        {
            routeBuilder.MapRoute("Plugin.Payments.BluePay.Rebilling",
                 "Plugins/PaymentBluePay/Rebilling",
                 new { controller = "PaymentBluePay", action = "Rebilling" });
        }

        public int Priority
        {
            get { return 0; }
        }
    }
}
