using GestPayServiceReference;
using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Tasks;
using Nop.Core.Plugins;
using Nop.Services.Cms;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Tasks;
using Nop.Services.Tax;
using Nop.Web.Framework.Infrastructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using System.Xml.Linq;
using static GestPayServiceReference.WSCryptDecryptSoapClient;

namespace Nop.Plugin.Payments.GestPay
{
    public class GestPayPaymentProcessor : BasePlugin, IPaymentMethod, IWidgetPlugin
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
        private readonly IPaymentService _paymentService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILocalizationService _localizationService;
        private readonly IScheduleTaskService _scheduleTaskService;
        #endregion

        #region Ctor

        public GestPayPaymentProcessor(GestPayPaymentSettings gestPayPaymentSettings,
            ISettingService settingService, ICurrencyService currencyService,
            CurrencySettings currencySettings, IWebHelper webHelper,
            ICheckoutAttributeParser checkoutAttributeParser, ITaxService taxService,
            IOrderTotalCalculationService orderTotalCalculationService,
            IPaymentService paymentService,
            IHttpContextAccessor httpContextAccessor,
            ILocalizationService localizationService,
            IScheduleTaskService scheduleTaskService)
        {
            _gestPayPaymentSettings = gestPayPaymentSettings;
            _settingService = settingService;
            _currencyService = currencyService;
            _currencySettings = currencySettings;
            _webHelper = webHelper;
            _checkoutAttributeParser = checkoutAttributeParser;
            _taxService = taxService;
            _orderTotalCalculationService = orderTotalCalculationService;
            _paymentService = paymentService;
            _httpContextAccessor = httpContextAccessor;
            _localizationService = localizationService;
            _scheduleTaskService = scheduleTaskService;
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

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentGestPay/Configure";
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
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.RedirectionTip", "Sarai ridirezionato al circuito di pagamento di BancaSella per completare il pagamento dell'ordine.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.UseSandbox", "Usa Ambiente di test");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.UseSandbox.Hint", "Spunta se vuoi abilitare l'ambiente di test.");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.UseStarter", "Usa GestPay Starter");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.UseStarter.Hint", "Spunta se vuoi indicare tipo account come Starter.");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.ShopOperatorCode", "Codice esercente");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.ShopOperatorCode.Hint", "Codice esercente di login. Es.: 0000001");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.AdditionalFee", "Costo aggiuntivo");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.AdditionalFee.Hint", "Inserisci il costo aggiuntivo che sarà accreditato al tuo cliente.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.AdditionalFeePercentage", "Costo aggiuntivo. Usa percentuale");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.AdditionalFeePercentage.Hint", "Determina se applicare un costo aggiuntivo in percentuale per l'importo totale dell'ordine. Se non selezionato, sarà applicato l'eventuale costo fisso.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.CurrencyUICcode", "Codice Valuta");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.CurrencyUICcode.Hint", "Codice UIC che verrà passato al sistema di pagamento per determinare la valuta in cui è passato la somma da pagare.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.LanguageCode", "Codice Lingua");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.LanguageCode.Hint", "Codice che determina la lingua dell'interfaccia mostrata all'utente.");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.ErrorMessage.PageTitle", "Attenzione!! si sono verificati degli errori.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.ErrorMessage.PageMessage00", "Impossibile procedere con il pagamento.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.ErrorMessage.PageMessage01", "La transazione ha avuto esito negativo.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.ErrorMessage.TitleSummary", "Riepilogo Problema:");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.ApiKey", "Api Key");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.ApiKey.Hint", "Enter Api Key");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.EnableGuaranteedPayment", "Enable Guaranteed Payment");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.GestPay.Fields.EnableGuaranteedPayment.Hint", "Only enable if Riskified API enable in your Gestpay account else contact gestpay support");

            base.Install();

            ScheduleTask task = new ScheduleTask();
            task.Enabled = true;
            task.Name = "Gestpay Verified Payment Check";
            task.Seconds = 180;
            task.StopOnError = false;
            task.Type = "Nop.Plugin.Payments.GestPay.Helper.RiskifiedStatusCheckScheduler, Nop.Plugin.Payments.GestPay";

            _scheduleTaskService.InsertTask(task);
        }

        /// <summary>
        /// Disinstallazione Plugin
        /// </summary>
        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<GestPayPaymentSettings>();

            //locales
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.RedirectionTip");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.UseSandbox");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.UseSandbox.Hint");

            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.UseStarter");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.UseStarter.Hint");

            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.ShopOperatorCode");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.ShopOperatorCode.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.AdditionalFee");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.AdditionalFee.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.AdditionalFeePercentage");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.AdditionalFeePercentage.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.CurrencyUICcode");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.CurrencyUICcode.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.LanguageCode");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.LanguageCode.Hint");

            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GestPay.ErrorMessage.PageTitle");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GestPay.ErrorMessage.PageMessage00");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GestPay.ErrorMessage.PageMessage01");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GestPay.ErrorMessage.TitleSummary");

            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.ApiKey");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.ApiKey.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.EnableGuaranteedPayment");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GestPay.Fields.EnableGuaranteedPayment.Hint");

            base.Uninstall();

            var task = _scheduleTaskService.GetTaskByType("Nop.Plugin.Payments.GestPay.Helper.RiskifiedStatusCheckScheduler, Nop.Plugin.Payments.GestPay");
            if (task != null)
                _scheduleTaskService.DeleteTask(task);
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


        public bool SupportCapture => false;

        public bool SupportPartiallyRefund => false;

        public bool SupportRefund => false;

        public bool SupportVoid => false;

        public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;

        public PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;

        public bool SkipPaymentInfo => false;

        public string PaymentMethodDescription => _localizationService.GetResource("Plugins.Payments.GestPay.PaymentMethodDescription");

        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            return new CancelRecurringPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            //let's ensure that at least 1 minute passed after order is placed
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalMinutes < 1)
                return false;

            return true;
        }

        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            return new CapturePaymentResult { Errors = new[] { "Capture method not supported" } };
        }

        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            var result = _paymentService.CalculateAdditionalFee(cart,
                _gestPayPaymentSettings.AdditionalFee, _gestPayPaymentSettings.AdditionalFeePercentage);
            return result;
        }

        public ProcessPaymentRequest GetPaymentInfo(IFormCollection form)
        {
            return new ProcessPaymentRequest();
        }

        public IList<string> GetWidgetZones()
        {
            return new List<string> { PublicWidgetZones.Footer };
        }

        public string GetWidgetViewComponentName(string widgetZone)
        {
            return "GestpayGuaranteedPayment";
        }

        public string GetPublicViewComponentName()
        {
            return "PaymentGestPay";
        }

        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            return false;
        }

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

            var endpoint = _gestPayPaymentSettings.UseSandbox ? EndpointConfiguration.WSCryptDecryptSoap12Test : EndpointConfiguration.WSCryptDecryptSoap12;
            var objCryptDecrypt = new WSCryptDecryptSoapClient(endpoint);

            XElement xmlResponse;

            EcommGestpayPaymentDetails paymentDetails = new EcommGestpayPaymentDetails();
            if (_gestPayPaymentSettings.EnableGuaranteedPayment)
            {
                FraudPrevention fraudPrevention = new FraudPrevention();
                fraudPrevention.BeaconSessionID = _httpContextAccessor.HttpContext.Session.Id;
                fraudPrevention.SubmitForReview = "1";
                fraudPrevention.OrderDateTime = postProcessPaymentRequest.Order.CreatedOnUtc.ToString();
                fraudPrevention.Source = "desktop_web";
                fraudPrevention.SubmissionReason = "rule_decision";
                fraudPrevention.VendorName = "Spinnaker";
                paymentDetails.FraudPrevention = fraudPrevention;

                //var logger = Nop.Core.Infrastructure.EngineContext.Current.Resolve<Nop.Services.Logging.ILogger>();
                //logger.Information("Gestpay BeaconId = " + _httpContextAccessor.HttpContext.Session.Id);

                CustomerDetail customerDetail = new CustomerDetail();
                customerDetail.PrimaryEmail = postProcessPaymentRequest.Order.BillingAddress.Email;
                customerDetail.MerchantCustomerID = postProcessPaymentRequest.Order.CustomerId.ToString();
                customerDetail.FirstName = postProcessPaymentRequest.Order.BillingAddress.FirstName;
                customerDetail.Lastname = postProcessPaymentRequest.Order.BillingAddress.LastName;
                customerDetail.PrimaryPhone = postProcessPaymentRequest.Order.BillingAddress.PhoneNumber;
                customerDetail.Company = postProcessPaymentRequest.Order.BillingAddress.Company;
                customerDetail.CreatedAtDate = postProcessPaymentRequest.Order.Customer.CreatedOnUtc.ToString();
                customerDetail.VerifiedEmail = "true";
                customerDetail.AccountType = "normal";
                paymentDetails.CustomerDetail = customerDetail;

                if (postProcessPaymentRequest.Order.ShippingAddress != null)
                {
                    ShippingAddress shippingAddress = new ShippingAddress();
                    shippingAddress.ProfileID = postProcessPaymentRequest.Order.ShippingAddressId.ToString();
                    shippingAddress.FirstName = postProcessPaymentRequest.Order.ShippingAddress.FirstName;
                    shippingAddress.Lastname = postProcessPaymentRequest.Order.ShippingAddress.LastName;
                    shippingAddress.StreetName = postProcessPaymentRequest.Order.ShippingAddress.Address1;
                    shippingAddress.Streetname2 = postProcessPaymentRequest.Order.ShippingAddress.Address2;
                    shippingAddress.City = postProcessPaymentRequest.Order.ShippingAddress.City;
                    shippingAddress.ZipCode = postProcessPaymentRequest.Order.ShippingAddress.ZipPostalCode;
                    shippingAddress.State = postProcessPaymentRequest.Order.ShippingAddress.StateProvince.Name;
                    shippingAddress.CountryCode = postProcessPaymentRequest.Order.ShippingAddress.Country.TwoLetterIsoCode;
                    shippingAddress.Email = postProcessPaymentRequest.Order.ShippingAddress.Email;
                    shippingAddress.PrimaryPhone = postProcessPaymentRequest.Order.ShippingAddress.PhoneNumber;
                    shippingAddress.Company = postProcessPaymentRequest.Order.ShippingAddress.Company;
                    shippingAddress.StateCode = postProcessPaymentRequest.Order.ShippingAddress.StateProvince.Abbreviation;
                    paymentDetails.ShippingAddress = shippingAddress;
                }

                BillingAddress billingAddress = new BillingAddress();
                billingAddress.ProfileID = postProcessPaymentRequest.Order.BillingAddressId.ToString();
                billingAddress.FirstName = postProcessPaymentRequest.Order.BillingAddress.FirstName;
                billingAddress.Lastname = postProcessPaymentRequest.Order.BillingAddress.LastName;
                billingAddress.StreetName = postProcessPaymentRequest.Order.BillingAddress.Address1;
                billingAddress.Streetname2 = postProcessPaymentRequest.Order.BillingAddress.Address2;
                billingAddress.City = postProcessPaymentRequest.Order.BillingAddress.City;
                billingAddress.ZipCode = postProcessPaymentRequest.Order.BillingAddress.ZipPostalCode;
                billingAddress.State = postProcessPaymentRequest.Order.BillingAddress.StateProvince.Name;
                billingAddress.CountryCode = postProcessPaymentRequest.Order.BillingAddress.Country.TwoLetterIsoCode;
                billingAddress.Email = postProcessPaymentRequest.Order.BillingAddress.Email;
                billingAddress.PrimaryPhone = postProcessPaymentRequest.Order.BillingAddress.PhoneNumber;
                billingAddress.Company = postProcessPaymentRequest.Order.BillingAddress.Company;
                billingAddress.StateCode = postProcessPaymentRequest.Order.BillingAddress.StateProvince.Abbreviation;
                paymentDetails.BillingAddress = billingAddress;

                var productDetails = new List<ProductDetail>();
                foreach (var item in postProcessPaymentRequest.Order.OrderItems)
                {
                    var productDetail = new ProductDetail();
                    productDetail.ProductCode = item.Product.ManufacturerPartNumber;
                    productDetail.SKU = item.Product.Sku;
                    productDetail.Name = item.Product.Name;
                    productDetail.Description = item.Product.ShortDescription;
                    productDetail.Quantity = item.Quantity.ToString();
                    productDetail.Price = item.PriceInclTax.ToString("0.00", CultureInfo.InvariantCulture);
                    productDetail.UnitPrice = item.UnitPriceInclTax.ToString("0.00", CultureInfo.InvariantCulture);

                    if ((!item.Product.IsGiftCard && item.Product.IsShipEnabled) || (item.Product.IsGiftCard && item.Product.GiftCardType == Core.Domain.Catalog.GiftCardType.Physical))
                    {
                        productDetail.Type = "physical";
                        productDetail.RequiresShipping = "true";
                    }
                    else
                    {
                        productDetail.Type = "digital";
                        productDetail.RequiresShipping = "false";

                        if (item.Product.IsGiftCard)
                        {
                            DigitalGiftCardDetails giftcardDetails = new DigitalGiftCardDetails();
                            foreach (var giftcard in item.AssociatedGiftCards)
                            {
                                giftcardDetails.SenderName = giftcard.SenderName;
                                giftcardDetails.DisplayName = giftcard.SenderName;
                                giftcardDetails.GreetingMessage = giftcard.Message;

                                Recipient recipient = new Recipient();
                                recipient.Email = giftcard.RecipientEmail;
                                giftcardDetails.Recipient = recipient;

                                break;
                            }
                            productDetail.DigitalGiftCardDetails = giftcardDetails;
                        }
                    }

                    productDetail.Vat = item.PriceInclTax > 0 ? "22" : "0";
                    productDetail.Condition = "new";
                    productDetail.Brand = item.Product.ProductManufacturers.FirstOrDefault()?.Manufacturer.Name;
                    //productDetail.DeliveryAt = "home";
                    productDetails.Add(productDetail);
                }
                paymentDetails.ProductDetails = productDetails.ToArray();

                var discountCode = new DiscountCode();
                //discountCode.Code = discount.Discount.CouponCode;
                discountCode.Amount = postProcessPaymentRequest.Order.OrderDiscount.ToString("0.00", CultureInfo.InvariantCulture);
                paymentDetails.DiscountCodes = new DiscountCode[] { discountCode };

                var shipping = new ShippingLine();
                shipping.Code = postProcessPaymentRequest.Order.ShippingRateComputationMethodSystemName;
                shipping.Title = postProcessPaymentRequest.Order.ShippingRateComputationMethodSystemName;
                shipping.Price = postProcessPaymentRequest.Order.OrderShippingInclTax.ToString("0.00", CultureInfo.InvariantCulture);
                paymentDetails.ShippingLines = new ShippingLine[] { shipping };
            }

            if (_gestPayPaymentSettings.UseStarter)
            {
                xmlResponse = objCryptDecrypt.EncryptAsync(
                          _gestPayPaymentSettings.ShopOperatorCode,
                          _gestPayPaymentSettings.CurrencyUiCcode.ToString(),
                          amount.ToString("0.00", CultureInfo.InvariantCulture),
                          shopTransactionId,
                          apikey: _gestPayPaymentSettings.ApiKey,
                          OrderDetails: paymentDetails
                      ).Result.EncryptResult;
            }
            else
            {
                xmlResponse = objCryptDecrypt.EncryptAsync(
                            _gestPayPaymentSettings.ShopOperatorCode,
                            _gestPayPaymentSettings.CurrencyUiCcode.ToString(),
                            amount.ToString("0.00", CultureInfo.InvariantCulture),
                            shopTransactionId,
                            buyerName: buyerName,
                            buyerEmail: postProcessPaymentRequest.Order.BillingAddress.Email,
                            languageId: _gestPayPaymentSettings.LanguageCode.ToString(),
                            apikey: _gestPayPaymentSettings.ApiKey,
                            OrderDetails: paymentDetails
                        ).Result.EncryptResult;
            }

            string errorCode = xmlResponse.Elements().Where(x => x.Name == "ErrorCode").Single().Value;
            if (errorCode == "0")
            {
                encryptedString = xmlResponse.Elements().Where(x => x.Name == "CryptDecryptString").Single().Value;
            }
            else
            {
                //Put error handle code HERE
                errorDescription = xmlResponse.Elements().Where(x => x.Name == "ErrorDescription").Single().Value;
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

            _httpContextAccessor.HttpContext.Response.Redirect(builder.ToString());
        }

        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            return new RefundPaymentResult { Errors = new[] { "Refund method not supported" } };
        }

        public IList<string> ValidatePaymentForm(IFormCollection form)
        {
            return new List<string>();
        }

        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            return new VoidPaymentResult { Errors = new[] { "Void method not supported" } };
            //return description of this payment method to be display on "payment method" checkout step. good practice is to make it localizable
            //for example, for a redirection payment method, description may be like this: "You will be redirected to site to complete the payment"
            get { return _localizationService.GetResource("Plugins.Payments.GestPay.PaymentMethodDescription"); }
        }

        #endregion
    }
}