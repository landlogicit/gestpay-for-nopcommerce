using GestPayServiceReference;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Tasks;
using Nop.Services.Catalog;
using Nop.Services.Cms;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;
using Nop.Services.Tasks;
using Nop.Services.Tax;
using Nop.Web.Framework.Infrastructure;
using OfficeOpenXml.ConditionalFormatting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using System.Xml;
using static GestPayServiceReference.WSCryptDecryptSoapClient;

namespace Nop.Plugin.Payments.GestPay
{
    public class GestPayPaymentProcessor : BasePlugin, IPaymentMethod, IWidgetPlugin
    {
        #region Fields

        private readonly GestPayPaymentSettings _gestPayPaymentSettings;
        private readonly IAddressService _addressService;
        private readonly ICustomerService _customerService;
        private readonly ICountryService _countryService;
        private readonly IGiftCardService _giftCardService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILocalizationService _localizationService;
        private readonly IManufacturerService _manufacturerService;
        private readonly IOrderService _orderService;
        private readonly IPaymentService _paymentService;
        private readonly IProductService _productService;
        private readonly IScheduleTaskService _scheduleTaskService;
        private readonly ISettingService _settingService;
        private readonly IStateProvinceService _stateProvinceService;
        private readonly IStoreContext _storeContext;
        private readonly IWebHelper _webHelper;

        #endregion

        #region Ctor

        public GestPayPaymentProcessor(GestPayPaymentSettings gestPayPaymentSettings,
            IAddressService addressService,
            ICustomerService customerService,
            ICountryService countryService,
            IGiftCardService giftCardService,
            IHttpContextAccessor httpContextAccessor,
            ILocalizationService localizationService,
            IManufacturerService manufacturerService,
            IOrderService orderService,
            IPaymentService paymentService,
            IProductService productService,
            IScheduleTaskService scheduleTaskService,
            ISettingService settingService,
            IStateProvinceService stateProvinceService,
            IStoreContext storeContext,
            IWebHelper webHelper)
        {
            _gestPayPaymentSettings = gestPayPaymentSettings;
            _addressService = addressService;
            _customerService = customerService;
            _countryService = countryService;
            _giftCardService = giftCardService;
            _httpContextAccessor = httpContextAccessor;
            _localizationService = localizationService;
            _manufacturerService = manufacturerService;
            _orderService = orderService;
            _paymentService = paymentService;
            _productService = productService;
            _scheduleTaskService = scheduleTaskService;
            _settingService = settingService;
            _stateProvinceService = stateProvinceService;
            _storeContext = storeContext;
            _webHelper = webHelper;
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
            _localizationService.AddPluginLocaleResource(new Dictionary<string, string>
            {
                ["Plugins.Payments.GestPay.Fields.RedirectionTip"] = "Sarai ridirezionato al circuito di pagamento di BancaSella per completare il pagamento dell'ordine.",
                ["Plugins.Payments.GestPay.Fields.UseSandbox"] = "Usa Ambiente di test",
                ["Plugins.Payments.GestPay.Fields.UseSandbox.Hint"] = "Spunta se vuoi abilitare l'ambiente di test.",

                ["Plugins.Payments.GestPay.Fields.UseStarter"] = "Usa GestPay Starter",
                ["Plugins.Payments.GestPay.Fields.UseStarter.Hint"] = "Spunta se vuoi indicare tipo account come Starter.",

                ["Plugins.Payments.GestPay.Fields.ShopOperatorCode"] = "Codice esercente",
                ["Plugins.Payments.GestPay.Fields.ShopOperatorCode.Hint"] = "Codice esercente di login. Es.: 0000001",
                ["Plugins.Payments.GestPay.Fields.AdditionalFee"] = "Costo aggiuntivo",
                ["Plugins.Payments.GestPay.Fields.AdditionalFee.Hint"] = "Inserisci il costo aggiuntivo che sarà accreditato al tuo cliente.",
                ["Plugins.Payments.GestPay.Fields.AdditionalFeePercentage"] = "Costo aggiuntivo. Usa percentuale",
                ["Plugins.Payments.GestPay.Fields.AdditionalFeePercentage.Hint"] = "Determina se applicare un costo aggiuntivo in percentuale per l'importo totale dell'ordine. Se non selezionato, sarà applicato l'eventuale costo fisso.",
                ["Plugins.Payments.GestPay.Fields.CurrencyUICcode"] = "Codice Valuta",
                ["Plugins.Payments.GestPay.Fields.CurrencyUICcode.Hint"] = "Codice UIC che verrà passato al sistema di pagamento per determinare la valuta in cui è passato la somma da pagare.",
                ["Plugins.Payments.GestPay.Fields.LanguageCode"] = "Codice Lingua",
                ["Plugins.Payments.GestPay.Fields.LanguageCode.Hint"] = "Codice che determina la lingua dell'interfaccia mostrata all'utente.",

                ["Plugins.Payments.GestPay.ErrorMessage.PageTitle"] = "Attenzione!! si sono verificati degli errori.",
                ["Plugins.Payments.GestPay.ErrorMessage.PageMessage00"] = "Impossibile procedere con il pagamento.",
                ["Plugins.Payments.GestPay.ErrorMessage.PageMessage01"] = "La transazione ha avuto esito negativo.",
                ["Plugins.Payments.GestPay.ErrorMessage.TitleSummary"] = "Riepilogo Problema:",

                ["Plugins.Payments.GestPay.Fields.ApiKey"] = "Api Key",
                ["Plugins.Payments.GestPay.Fields.ApiKey.Hint"] = "Enter Api Key",
                ["Plugins.Payments.GestPay.Fields.EnableGuaranteedPayment"] = "Enable Guaranteed Payment",
                ["Plugins.Payments.GestPay.Fields.EnableGuaranteedPayment.Hint"] = "Only enable if Riskified API enable in your Gestpay account else contact gestpay support",
                ["Plugins.Payments.GestPay.PaymentMethodDescription"] = "Verrai reindirizzato al sito GestPay per completare il pagamento",
            });

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
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.GestPay");

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

        public bool HideInWidgetList => false;

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
            return new List<string> {
                PublicWidgetZones.Footer,
                AdminWidgetZones.OrderDetailsButtons
            };
        }

        public string GetWidgetViewComponentName(string widgetZone)
        {
            if (widgetZone == PublicWidgetZones.Footer)
                return "GestpayGuaranteedPayment";
            if (widgetZone == AdminWidgetZones.OrderDetailsButtons)
                return "GestpayPaymentLink";
            return "";
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

            var nopBillingAddress = _addressService.GetAddressById(postProcessPaymentRequest.Order.BillingAddressId);

            var amount = Math.Round(postProcessPaymentRequest.Order.OrderTotal, 2);
            var shopTransactionId = postProcessPaymentRequest.Order.OrderGuid.ToString();
            var buyerName = String.Format(
                "{0} {1}",
                nopBillingAddress?.FirstName,
                nopBillingAddress?.LastName
            );

            var endpoint = _gestPayPaymentSettings.UseSandbox ? EndpointConfiguration.WSCryptDecryptSoap12Test : EndpointConfiguration.WSCryptDecryptSoap12;
            var objCryptDecrypt = new WSCryptDecryptSoapClient(endpoint);

            var billingCountry = _countryService.GetCountryById(Convert.ToInt32(nopBillingAddress.CountryId));
            var billingStateProvince = _stateProvinceService.GetStateProvinceByAddress(nopBillingAddress);

            Address nopShippingAddress = null;
            Country shippingCountry = null;
            StateProvince shippingStateProvince = null;
            if (postProcessPaymentRequest.Order.ShippingAddressId != null)
            {
                nopShippingAddress = _addressService.GetAddressById((int)postProcessPaymentRequest.Order.ShippingAddressId);
                shippingCountry = _countryService.GetCountryById((int)nopShippingAddress.CountryId);
                shippingStateProvince = _stateProvinceService.GetStateProvinceByAddress(nopShippingAddress);
            }

            XmlNode xmlResponse;
            EcommGestpayPaymentDetails paymentDetails = new EcommGestpayPaymentDetails();
            if (_gestPayPaymentSettings.EnableGuaranteedPayment)
            {
                FraudPrevention fraudPrevention = new FraudPrevention();
                fraudPrevention.BeaconSessionID = _httpContextAccessor.HttpContext.Session.Id;
                fraudPrevention.SubmitForReview = "1";
                fraudPrevention.OrderDateTime = postProcessPaymentRequest.Order.CreatedOnUtc.ToString();
                fraudPrevention.Source = "desktop_web";
                fraudPrevention.SubmissionReason = "rule_decision";
                fraudPrevention.VendorName = _storeContext.CurrentStore.Name;
                paymentDetails.FraudPrevention = fraudPrevention;

                //var logger = Nop.Core.Infrastructure.EngineContext.Current.Resolve<Nop.Services.Logging.ILogger>();
                //logger.Information("Gestpay BeaconId = " + _httpContextAccessor.HttpContext.Session.Id);

                var customer = _customerService.GetCustomerById(postProcessPaymentRequest.Order.CustomerId);
                CustomerDetail customerDetail = new CustomerDetail();
                customerDetail.PrimaryEmail = nopBillingAddress?.Email;
                customerDetail.MerchantCustomerID = postProcessPaymentRequest.Order.CustomerId.ToString();
                customerDetail.FirstName = nopBillingAddress?.FirstName;
                customerDetail.Lastname = nopBillingAddress?.LastName;
                customerDetail.PrimaryPhone = nopBillingAddress?.PhoneNumber;
                customerDetail.Company = nopBillingAddress?.Company;
                customerDetail.CreatedAtDate = customer?.CreatedOnUtc.ToString();
                customerDetail.VerifiedEmail = "true";
                customerDetail.AccountType = "normal";
                paymentDetails.CustomerDetail = customerDetail;

                if (nopShippingAddress != null)
                {
                    ShippingAddress shippingAddress = new ShippingAddress();
                    shippingAddress.ProfileID = postProcessPaymentRequest.Order.ShippingAddressId.ToString();
                    shippingAddress.FirstName = nopShippingAddress.FirstName;
                    shippingAddress.Lastname = nopShippingAddress.LastName;
                    shippingAddress.StreetName = nopShippingAddress.Address1;
                    shippingAddress.Streetname2 = nopShippingAddress.Address2;
                    shippingAddress.City = nopShippingAddress.City;
                    shippingAddress.ZipCode = nopShippingAddress.ZipPostalCode;
                    shippingAddress.State = shippingStateProvince?.Name;
                    shippingAddress.CountryCode = shippingCountry?.TwoLetterIsoCode;
                    shippingAddress.Email = nopShippingAddress.Email;
                    shippingAddress.PrimaryPhone = nopShippingAddress.PhoneNumber;
                    shippingAddress.Company = nopShippingAddress.Company;
                    shippingAddress.StateCode = shippingStateProvince?.Abbreviation;
                    paymentDetails.ShippingAddress = shippingAddress;
                }

                BillingAddress billingAddress = new BillingAddress();
                billingAddress.ProfileID = postProcessPaymentRequest.Order.BillingAddressId.ToString();
                billingAddress.FirstName = nopBillingAddress?.FirstName;
                billingAddress.Lastname = nopBillingAddress?.LastName;
                billingAddress.StreetName = nopBillingAddress?.Address1;
                billingAddress.Streetname2 = nopBillingAddress?.Address2;
                billingAddress.City = nopBillingAddress?.City;
                billingAddress.ZipCode = nopBillingAddress?.ZipPostalCode;
                billingAddress.State = billingStateProvince?.Name;
                billingAddress.CountryCode = billingCountry?.TwoLetterIsoCode;
                billingAddress.Email = nopBillingAddress?.Email;
                billingAddress.PrimaryPhone = nopBillingAddress?.PhoneNumber;
                billingAddress.Company = nopBillingAddress?.Company;
                billingAddress.StateCode = billingStateProvince?.Abbreviation;
                paymentDetails.BillingAddress = billingAddress;

                var orderItems = _orderService.GetOrderItems(postProcessPaymentRequest.Order.Id);
                var productDetails = new List<ProductDetail>();
                decimal itemsTotalInclTax = 0;
                foreach (var item in orderItems)
                {
                    var product = _productService.GetProductById(item.ProductId);

                    if (product != null)
                    {
                        var productDetail = new ProductDetail();
                        productDetail.ProductCode = product.ManufacturerPartNumber;
                        productDetail.SKU = product.Sku;
                        productDetail.Name = product.Name;
                        productDetail.Description = product.ShortDescription;
                        productDetail.Quantity = item.Quantity.ToString();
                        productDetail.Price = item.PriceInclTax.ToString("0.00", CultureInfo.InvariantCulture);
                        productDetail.UnitPrice = item.UnitPriceInclTax.ToString("0.00", CultureInfo.InvariantCulture);

                        if ((!product.IsGiftCard && product.IsShipEnabled) || (product.IsGiftCard && product.GiftCardType == Core.Domain.Catalog.GiftCardType.Physical))
                        {
                            productDetail.Type = "physical";
                            productDetail.RequiresShipping = "true";
                        }
                        else
                        {
                            productDetail.Type = "digital";
                            productDetail.RequiresShipping = "false";

                            if (product.IsGiftCard)
                            {
                                DigitalGiftCardDetails giftcardDetails = new DigitalGiftCardDetails();
                                var associatedGiftCards = _giftCardService.GetAllGiftCards(postProcessPaymentRequest.Order.Id);
                                foreach (var giftcard in associatedGiftCards)
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

                        var productManufacturers = _manufacturerService.GetProductManufacturersByProductId(product.Id);
                        productDetail.Brand = _manufacturerService.GetManufacturerById((int)productManufacturers.FirstOrDefault()?.ManufacturerId)?.Name;
                        //productDetail.DeliveryAt = "home";
                        productDetails.Add(productDetail);

                        itemsTotalInclTax += item.UnitPriceInclTax * item.Quantity;
                    }
                }
                paymentDetails.ProductDetails = productDetails.ToArray();

                IList<DiscountCode> discountCodes = new List<DiscountCode>();
                IList<ShippingLine> shippingLines = new List<ShippingLine>();

                //  Discount on Sub Total
                var subTotalDiscountCode = new DiscountCode();
                subTotalDiscountCode.Code = "Order subtotal discount";
                subTotalDiscountCode.Amount = postProcessPaymentRequest.Order.OrderSubTotalDiscountInclTax.ToString("0.00", CultureInfo.InvariantCulture);
                discountCodes.Add(subTotalDiscountCode);

                //  Discount on Total
                var discountCode = new DiscountCode();
                discountCode.Code = "Order total discount";
                discountCode.Amount = postProcessPaymentRequest.Order.OrderDiscount.ToString("0.00", CultureInfo.InvariantCulture);
                discountCodes.Add(discountCode);

                //  Shipping
                var shipping = new ShippingLine();
                shipping.Code = postProcessPaymentRequest.Order.ShippingRateComputationMethodSystemName;
                shipping.Title = postProcessPaymentRequest.Order.ShippingRateComputationMethodSystemName;
                shipping.Price = postProcessPaymentRequest.Order.OrderShippingInclTax.ToString("0.00", CultureInfo.InvariantCulture);
                shippingLines.Add(shipping);

                //  Additional Charges / GiftWrap / GiftCard
                var extraAdjustment = postProcessPaymentRequest.Order.OrderTotal - (itemsTotalInclTax + postProcessPaymentRequest.Order.OrderShippingInclTax - postProcessPaymentRequest.Order.OrderDiscount - postProcessPaymentRequest.Order.OrderSubTotalDiscountInclTax);
                if (extraAdjustment > 0)
                {
                    var additionalShipping = new ShippingLine();
                    additionalShipping.Code = "Additional Charge/Giftwrap";
                    additionalShipping.Title = "Additional Charge/Giftwrap";
                    additionalShipping.Price = extraAdjustment.ToString("0.00", CultureInfo.InvariantCulture);
                    shippingLines.Add(additionalShipping);
                }
                else
                {
                    var additionalDiscount = new DiscountCode();
                    additionalDiscount.Code = "Giftcard/Additional Discount";
                    additionalDiscount.Amount = (extraAdjustment * -1).ToString("0.00", CultureInfo.InvariantCulture);      // * -1 converts into positive number 
                    discountCodes.Add(additionalDiscount);
                }

                paymentDetails.DiscountCodes = discountCodes.ToArray();
                paymentDetails.ShippingLines = shippingLines.ToArray();
            }

            //  3DS
            var threeDSTransDetails = new ThreeDSEncryptTransDetails();
            threeDSTransDetails.type = "EC";
            threeDSTransDetails.authenticationAmount = amount.ToString("0.00", CultureInfo.InvariantCulture);

            var threeDSContainer = new EncryptThreeDsContainer();
            threeDSContainer.transTypeReq = "P";
            //threeDSContainer.exemption = "SKIP";  As asked by Gestpay Support

            BuyerDetails buyerDetails = new BuyerDetails();

            ThreeDSBillingAddress threeDSBillingAddress = new ThreeDSBillingAddress();
            threeDSBillingAddress.line1 = nopBillingAddress?.Address1;
            threeDSBillingAddress.line2 = nopBillingAddress?.Address2;
            threeDSBillingAddress.city = nopBillingAddress?.City;
            threeDSBillingAddress.postCode = nopBillingAddress?.ZipPostalCode;
            threeDSBillingAddress.state = billingStateProvince?.Name;
            threeDSBillingAddress.country = billingCountry?.TwoLetterIsoCode;
            buyerDetails.billingAddress = threeDSBillingAddress;

            if (nopShippingAddress != null)
            {
                ThreeDSShippingAddress threeDSShippingAddress = new ThreeDSShippingAddress();
                threeDSShippingAddress.line1 = nopShippingAddress?.Address1;
                threeDSShippingAddress.line2 = nopShippingAddress?.Address2;
                threeDSShippingAddress.city = nopShippingAddress?.City;
                threeDSShippingAddress.postCode = nopShippingAddress?.ZipPostalCode;
                threeDSShippingAddress.state = shippingStateProvince?.Name;
                threeDSShippingAddress.country = shippingCountry?.TwoLetterIsoCode;
                buyerDetails.shippingAddress = threeDSShippingAddress;
            }

            buyerDetails.addrMatch = "N";

            threeDSContainer.buyerDetails = buyerDetails;
            threeDSTransDetails.threeDsContainer = threeDSContainer;

            xmlResponse = objCryptDecrypt.EncryptAsync(
                     _gestPayPaymentSettings.ShopOperatorCode,
                     _gestPayPaymentSettings.CurrencyUiCcode.ToString(),
                     amount.ToString("0.00", CultureInfo.InvariantCulture),
                     shopTransactionId,
                     "", "", "", buyerName, nopBillingAddress.Email,
                     _gestPayPaymentSettings.LanguageCode.ToString(), "",
                     "Order Number = " + postProcessPaymentRequest.Order.CustomOrderNumber, "", "", "",
                     null,
                     null,
                     null, "",
                     null, null, null, null, null, null, "", null, "", "",
                     paymentDetails, _gestPayPaymentSettings.ApiKey, threeDSTransDetails).Result.EncryptResult;

            XmlDocument xmlReturn = new XmlDocument();
            xmlReturn.LoadXml(xmlResponse.OuterXml);

            string errorCode = xmlReturn.SelectSingleNode("/GestPayCryptDecrypt/ErrorCode")?.InnerText;

            if (errorCode == "0")
            {
                encryptedString = xmlReturn.SelectSingleNode("/GestPayCryptDecrypt/CryptDecryptString")?.InnerText;
            }
            else
            {
                //Put error handle code HERE
                errorDescription = xmlReturn.SelectSingleNode("/GestPayCryptDecrypt/ErrorDescription")?.InnerText;
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
        }

        #endregion
    }
}