using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Payments;
using Nop.Web.Areas.Admin.Models.Orders;
using Nop.Web.Framework.Components;
using System;
using System.Collections.Generic;
using System.Text;

namespace Nop.Plugin.Payments.GestPay.Components
{
    public class GestpayPaymentLinkViewComponent : NopViewComponent
    {
        public IViewComponentResult Invoke(string widgetZone, object additionalData)
        {
            var receivedModel = (OrderModel)additionalData;

            if (receivedModel.CanMarkOrderAsPaid && receivedModel.PaymentStatus == PaymentStatus.Pending.ToString())
                return View("~/Plugins/Payments.GestPay/Views/PaymentLink.cshtml", receivedModel.Id);
            else
                return Content("");
        }
    }
}
