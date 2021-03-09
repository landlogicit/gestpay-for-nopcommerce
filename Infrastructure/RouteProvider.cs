using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.GestPay.Infrastructure
{
    public class RouteProvider : IRouteProvider
    {
        public int Priority => -1;

        public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
        {
            //Esito
            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.GestPay.EsitoGestPay", "Plugins/PaymentGestPay/EsitoGestPay/{esito?}",
                new { controller = "PaymentGestPay", action = "EsitoGestPay" });

            //Esito 2
            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.GestPay.AcceptPaymenyByLink", "Plugins/PaymentGestPay/AcceptPaymenyByLink/{a}/{status}/{paymentId}/{paymentToken}",
                new { controller = "PaymentGestPay", action = "AcceptPaymenyByLink" });

            //s2s
            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.GestPay.s2sHandler", "Plugins/PaymentGestPay/s2sHandler",
                new { controller = "PaymentGestPay", action = "s2sHandler" });

            //Cancel
            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.GestPay.CancelOrder", "Plugins/PaymentGestPay/CancelOrder",
                new { controller = "PaymentGestPay", action = "CancelOrder" });

            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.GestPay.GeneralError", "Plugins/PaymentGestPay/Error",
                new { controller = "PaymentGestPay", action = "GeneralError" });
        }
    }
}
