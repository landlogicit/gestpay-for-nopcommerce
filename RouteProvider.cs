using System.Web.Mvc;
using System.Web.Routing;
using Nop.Web.Framework.Mvc.Routes;

namespace Nop.Plugin.Payments.GestPay
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(RouteCollection routes)
        {
              //Esito
            routes.MapRoute("Plugin.Payments.GestPay.EsitoGestPay",
                 "Plugins/PaymentGestPay/EsitoGestPay/{esito}",
                 new { controller = "PaymentGestPay", action = "EsitoGestPay", esito = UrlParameter.Optional },
                 new[] { "Nop.Plugin.Payments.GestPay.Controllers" }
            );
            //s2s
            routes.MapRoute("Plugin.Payments.GestPay.s2sHandler",
                 "Plugins/PaymentGestPay/s2sHandler",
                 new { controller = "PaymentGestPay", action = "s2sHandler" },
                 new[] { "Nop.Plugin.Payments.GestPay.Controllers" }
            );
            //Cancel
            routes.MapRoute("Plugin.Payments.GestPay.CancelOrder",
                 "Plugins/PaymentGestPay/CancelOrder",
                 new { controller = "PaymentGestPay", action = "CancelOrder" },
                 new[] { "Nop.Plugin.Payments.GestPay.Controllers" }
            );
            //Errore generico
            routes.MapRoute("Plugin.Payments.GestPay.Error",
                 "Plugins/PaymentGestPay/Error",
                 new { controller = "PaymentGestPay", action = "GeneralError" },
                 new[] { "Nop.Plugin.Payments.GestPay.Controllers" }
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
