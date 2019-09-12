using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Web.Framework.Components;
using System;
using System.Collections.Generic;
using System.Text;

namespace Nop.Plugin.Payments.GestPay.Component
{
    [ViewComponent(Name = "GestpayGuaranteedPayment")]
    public class GestpayGuaranteedPaymentViewComponent : NopViewComponent
    {
        private readonly GestPayPaymentSettings _gestPayPaymentSettings;
        private readonly IStoreContext _storeContext;

        public GestpayGuaranteedPaymentViewComponent(GestPayPaymentSettings gestPayPaymentSettings,
            IStoreContext storeContext)
        {
            _gestPayPaymentSettings = gestPayPaymentSettings;
            _storeContext = storeContext;
        }

        public IViewComponentResult Invoke()
        {
            var url = new Uri(_storeContext.CurrentStore.Url);
            ViewBag.StoreDomain = url.Host;
            ViewBag.EnableGuaranteedPayment = _gestPayPaymentSettings.EnableGuaranteedPayment;

            return View("~/Plugins/Payments.GestPay/Views/PaymentGestPay/GestpayGuaranteedPayment.cshtml");
        }
    }
}
