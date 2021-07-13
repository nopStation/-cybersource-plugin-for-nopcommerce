using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.CyberSource
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
        {
            //IPN
            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.CyberSource.IPNHandler",
                 "Plugins/PaymentCyberSource/IPNHandler",
                 new { controller = "PaymentCyberSource", action = "IPNHandler" });
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
