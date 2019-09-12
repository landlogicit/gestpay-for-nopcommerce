using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;
using System;
using System.Collections.Generic;
using System.Text;

namespace Nop.Plugin.Payments.GestPay.Infrastructure
{
    public class RouteProvider : IRouteProvider
    {
        public int Priority => -1;

        public void RegisterRoutes(IRouteBuilder routeBuilder)
        {
            //Esito
            routeBuilder.MapRoute("Plugin.Payments.GestPay.EsitoGestPay", "Plugins/PaymentGestPay/EsitoGestPay/{esito?}",
                new { controller = "PaymentGestPay", action = "EsitoGestPay"});

            //s2s
            routeBuilder.MapRoute("Plugin.Payments.GestPay.s2sHandler", "Plugins/PaymentGestPay/s2sHandler",
                new { controller = "PaymentGestPay", action = "s2sHandler" });

            //Cancel
            routeBuilder.MapRoute("Plugin.Payments.GestPay.CancelOrder", "Plugins/PaymentGestPay/CancelOrder",
                new { controller = "PaymentGestPay", action = "CancelOrder" });

            routeBuilder.MapRoute("Plugin.Payments.GestPay.GeneralError", "Plugins/PaymentGestPay/Error",
                new { controller = "PaymentGestPay", action = "GeneralError" });
        }
    }
}
