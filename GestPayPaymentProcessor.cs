using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Plugins;
using Nop.Plugin.Payments.GestPay.Controllers;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Tax;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Web;
using System.Web.Routing;
using System.Xml;

namespace Nop.Plugin.Payments.GestPay
{
    /// <summary>
    /// GestPay payment processor
    /// </summary>
    public class GestPayPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly GestPayPaymentSettings _gestPayPaymentSettings;
        private readonly ISettingService _settingService;
        private readonly ICurrencyService _currencyService;
        private readonly CurrencySettings _currencySettings;
        private readonly IWebHelper _webHelper;
        private readonly ICheckoutAttributeParser _checkoutAttributeParser;
        private readonly ITaxService _taxService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly HttpContextBase _httpContext;
        private readonly ILocalizationService _localizationService;
        #endregion

        #region Ctor

        public GestPayPaymentProcessor(GestPayPaymentSettings gestPayPaymentSettings,
            ISettingService settingService, ICurrencyService currencyService,
            CurrencySettings currencySettings, IWebHelper webHelper,
            ICheckoutAttributeParser checkoutAttributeParser, ITaxService taxService,
            IOrderTotalCalculationService orderTotalCalculationService, HttpContextBase httpContext,
            ILocalizationService localizationService)
        {
            _gestPayPaymentSettings = gestPayPaymentSettings;
            _settingService = settingService;
            _currencyService = currencyService;
            _currencySettings = currencySettings;
            _webHelper = webHelper;
            _checkoutAttributeParser = checkoutAttributeParser;
            _taxService = taxService;
            _orderTotalCalculationService = orderTotalCalculationService;
            _httpContext = httpContext;
            _localizationService = localizationService;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Gets GestPay URL
        /// </summary>
        /// <returns></returns>
        private string GetGestPayUrl()
        {
            return _gestPayPaymentSettings.UseSandbox ? "https://sandbox.gestpay.net/pagam/pagam.aspx" :
                "https://ecomm.sella.it/pagam/pagam.aspx";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="formString">Form string</param>
        /// <param name="values">Values</param>
        public void GetResponseDetails(string formString, out Dictionary<string, string> values)
        {
            values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in formString.Split('&'))
            {
                var line = l.Trim();
                var equalPox = line.IndexOf('=');
                if (equalPox >= 0)
                    values.Add(line.Substring(0, equalPox), line.Substring(equalPox + 1));
            }
        }

        /// <summary>
        /// Controllo che il codice sia effettivamente quello dell'esercente in questione
        /// </summary>
        /// <param name="shopLogin"></param>
        /// <returns></returns>
        public bool IsShopLoginChecked(string shopLogin)
        {
            if (String.IsNullOrEmpty(shopLogin))
                return false;
            else
                return (shopLogin == _gestPayPaymentSettings.ShopOperatorCode);
        }

        /// <summary>
        /// Ritorna se si è in ambiente di test o di produzione
        /// </summary>
        /// <returns></returns>
        public bool UseSandboxEnvironment()
        {
            return _gestPayPaymentSettings.UseSandbox;
        }

        #endregion

        #region Methods

        public Type GetControllerType()
        {
            return typeof(PaymentGestPayController);
        }

        /// <summary>
        /// Installazione Plugin
        /// </summary>
        public override void Install()
        {
            //settings
            var settings = new GestPayPaymentSettings()
            {
                UseSandbox = true,
                ShopOperatorCode = "9000001",
                LanguageCode = 1,
                CurrencyUiCcode = 242,
            };
            _settingService.SaveSetting(settings);

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.RedirectionTip", "Sarai ridirezionato al circuito di pagamento di BancaSella per completare il pagamento dell'ordine.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.UseSandbox", "Usa Ambiente di test");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.UseSandbox.Hint", "Spunta se vuoi abilitare l'ambiente di test.");

            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.UseStarter", "Usa GestPay Starter");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.UseStarter.Hint", "Spunta se vuoi indicare tipo account come Starter.");


            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.ShopOperatorCode", "Codice esercente");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.ShopOperatorCode.Hint", "Codice esercente di login. Es.: 0000001");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.AdditionalFee", "Costo aggiuntivo");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.AdditionalFee.Hint", "Inserisci il costo aggiuntivo che sarà accreditato al tuo cliente.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.AdditionalFeePercentage", "Costo aggiuntivo. Usa percentuale");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.AdditionalFeePercentage.Hint", "Determina se applicare un costo aggiuntivo in percentuale per l'importo totale dell'ordine. Se non selezionato, sarà applicato l'eventuale costo fisso.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.CurrencyUICcode", "Codice Valuta");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.CurrencyUICcode.Hint", "Codice UIC che verrà passato al sistema di pagamento per determinare la valuta in cui è passato la somma da pagare.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.LanguageCode", "Codice Lingua");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.LanguageCode.Hint", "Codice che determina la lingua dell'interfaccia mostrata all'utente.");

            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.ErrorMessage.PageTitle", "Attenzione!! si sono verificati degli errori.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.ErrorMessage.PageMessage00", "Impossibile procedere con il pagamento.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.ErrorMessage.PageMessage01", "La transazione ha avuto esito negativo.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.ErrorMessage.TitleSummary", "Riepilogo Problema:");

            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.ApiKey", "Api Key");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.ApiKey.Hint", "Enter Api Key");

            base.Install();
        }

        /// <summary>
        /// Disinstallazione Plugin
        /// </summary>
        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<GestPayPaymentSettings>();

            //locales
            this.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.RedirectionTip");
            this.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.UseSandbox");
            this.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.UseSandbox.Hint");

            this.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.UseStarter");
            this.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.UseStarter.Hint");

            this.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.ShopOperatorCode");
            this.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.ShopOperatorCode.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.AdditionalFee");
            this.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.AdditionalFee.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.AdditionalFeePercentage");
            this.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.AdditionalFeePercentage.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.CurrencyUICcode");
            this.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.CurrencyUICcode.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.LanguageCode");
            this.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.LanguageCode.Hint");

            this.DeletePluginLocaleResource("Plugins.Payments.GestPay.ErrorMessage.PageTitle");
            this.DeletePluginLocaleResource("Plugins.Payments.GestPay.ErrorMessage.PageMessage00");
            this.DeletePluginLocaleResource("Plugins.Payments.GestPay.ErrorMessage.PageMessage01");
            this.DeletePluginLocaleResource("Plugins.Payments.GestPay.ErrorMessage.TitleSummary");

            base.Uninstall();
        }

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult { NewPaymentStatus = PaymentStatus.Pending };
            return result;
        }

        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            return false;
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            var result = this.CalculateAdditionalFee(_orderTotalCalculationService, cart,
                _gestPayPaymentSettings.AdditionalFee, _gestPayPaymentSettings.AdditionalFeePercentage);
            return result;
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            result.AddError("Capture method not supported");
            return result;
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            result.AddError("Refund method not supported");
            return result;
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            result.AddError("Void method not supported");
            return result;
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            //let's ensure that at least 1 minute passed after order is placed
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalMinutes < 1)
                return false;

            return true;
        }

        /// <summary>
        /// Gets a route for provider configuration
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "PaymentGestPay";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Payments.GestPay.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Gets a route for payment info
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "PaymentGestPay";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Payments.GestPay.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            /*
             * Dati transazione inviati verso GestPay
             * Alcuni dei parametri "importanti/principali" per creazione della stringa 
             * "ShopLogin"         :VarChar (30) - Obbligatorio - Codice Esercente (Shop Login)
             * "Currency"          :Num (3) - Obbligatorio - Codice Identificativo della divisa per l'importo della transazione
             * "Amount"            :Num (9) - Obbligatorio - [ll separatore delle migliaia non deve essere inserito. I decimali (max 2 cifre) sono opzionali ed il separatore è il punto.]
             * "ShopTransactionID" :VarChar (50) - Obbligatorio - Identificativo attribuito alla transazione dall'esercente.
             * "BuyerName"         :VarChar (50) - Facoltativo - Nome e cognome dell'acquirente
             * "BuyerEmail"        :VarChar (50) - Facoltativo - Indirizzo e-mail dell'acquirente
             * "Language"          :Num (2) - Facoltativo - Codice che identifica la lingua utilizzata nella comunicazione con l'acquirente (vedi tabella Codici lingua).
            */
            var encryptedString = "";
            var errorDescription = "";

            //HttpUtility.UrlEncode(_currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId).CurrencyCode)));
            var amount = Math.Round(postProcessPaymentRequest.Order.OrderTotal, 2);
            var shopTransactionId = postProcessPaymentRequest.Order.OrderGuid.ToString();
            var buyerName = String.Format(
                "{0} {1}",
                postProcessPaymentRequest.Order.BillingAddress.FirstName,
                postProcessPaymentRequest.Order.BillingAddress.LastName
            );

            var objCryptDecrypt = new WSCryptDecrypt(UseSandboxEnvironment());

            string xmlResponse;

            if (_gestPayPaymentSettings.UseStarter)
            {
                xmlResponse = objCryptDecrypt.Encrypt(
                           _gestPayPaymentSettings.ShopOperatorCode,
                           _gestPayPaymentSettings.CurrencyUiCcode.ToString(),
                           amount.ToString("0.00", CultureInfo.InvariantCulture),
                           shopTransactionId,
                           apikey: _gestPayPaymentSettings.ApiKey
                       ).OuterXml;
            }
            else
            {
                xmlResponse = objCryptDecrypt.Encrypt(
                            _gestPayPaymentSettings.ShopOperatorCode,
                            _gestPayPaymentSettings.CurrencyUiCcode.ToString(),
                            amount.ToString("0.00", CultureInfo.InvariantCulture),
                            shopTransactionId,
                            buyerName: buyerName,
                            buyerEmail: postProcessPaymentRequest.Order.BillingAddress.Email,
                            languageId: _gestPayPaymentSettings.LanguageCode.ToString(),
                            apikey: _gestPayPaymentSettings.ApiKey
                        ).OuterXml;
            }

            var xmlReturn = new XmlDocument();
            xmlReturn.LoadXml(xmlResponse);
            var thisNode = xmlReturn.SelectSingleNode("/GestPayCryptDecrypt/ErrorCode");
            string errorCode = thisNode.InnerText;
            if (errorCode == "0")
            {
                var ThisNode2 = xmlReturn.SelectSingleNode("/GestPayCryptDecrypt/CryptDecryptString");
                encryptedString = ThisNode2.InnerText;
            }
            else
            {
                //Put error handle code HERE
                thisNode = xmlReturn.SelectSingleNode("/GestPayCryptDecrypt/ErrorDescription");
                errorDescription = thisNode.InnerText;
            }

            var builder = new StringBuilder();

            if (!String.IsNullOrEmpty(encryptedString))
            {
                builder.Append(GetGestPayUrl());
                builder.AppendFormat("?a={0}&b={1}", _gestPayPaymentSettings.ShopOperatorCode, encryptedString);
            }
            else
            {
                //Errore
                builder.Append(_webHelper.GetStoreLocation(false) + "Plugins/PaymentGestPay/Error");
                builder.AppendFormat("?type=0&errc={0}&errd={1}", HttpUtility.UrlEncode(errorCode), HttpUtility.UrlEncode(errorDescription));
            }

            _httpContext.Response.Redirect(builder.ToString());
        }
        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get
            {
                return RecurringPaymentType.NotSupported;
            }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get
            {
                return PaymentMethodType.Redirection;
            }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public string PaymentMethodDescription
        {
            //return description of this payment method to be display on "payment method" checkout step. good practice is to make it localizable
            //for example, for a redirection payment method, description may be like this: "You will be redirected to site to complete the payment"
            get { return _localizationService.GetResource("Plugins.Payments.GestPay.PaymentMethodDescription"); }
        }

        #endregion
    }
}
