using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.GestPay.Component
{
    [ViewComponent(Name = "PaymentGestPay")]
    public class PaymentGestPayViewComponent : NopViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View("~/Plugins/Payments.GestPay/Views/PaymentGestPay/PaymentInfo.cshtml");
        }
    }
}
