using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
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
using Nop.Web.Framework.Infrastructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Task = System.Threading.Tasks.Task;
using GestPayServiceReference;
using GestPayWsS2SServiceReference;
using Nop.Core.Domain.ScheduleTasks;
using Nop.Plugin.Payments.GestPay.Components;
using Nop.Services.ScheduleTasks;
using static GestPayServiceReference.WSCryptDecryptSoapClient;
namespace Nop.Plugin.Payments.GestPay
{
    public class GestPayPaymentProcessor : BasePlugin, IPaymentMethod, IWidgetPlugin
    {
        #region Fields

        private readonly GestPayPaymentSettings _gestPayPaymentSettings;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
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
            IStoreContext storeContext,IOrderTotalCalculationService orderTotalCalculationService,
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
            _orderTotalCalculationService = orderTotalCalculationService;
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

        private WSs2sSoapClient GetS2SClient()
        {
            var endpoint = _gestPayPaymentSettings.UseSandbox ? WSs2sSoapClient.EndpointConfiguration.WSs2sSoap12Test : WSs2sSoapClient.EndpointConfiguration.WSs2sSoap12;
            return new WSs2sSoapClient(endpoint);
        }

        private string GetValueFromXml(XmlNode xml, string xpath)
        {
            return xml.SelectSingleNode(xpath)?.InnerText;
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

        public Type GetPublicViewComponent()
        {
            return typeof(PaymentGestPayViewComponent);
        }

        public async Task<string> GetPaymentMethodDescriptionAsync()
        {
            return await _localizationService.GetResourceAsync("Plugins.Payments.GestPay.PaymentMethodDescription");
        }
        /// <summary>
        /// Installazione Plugin
        /// </summary>
        public override async Task InstallAsync()
        {
            //settings
            var settings = new GestPayPaymentSettings()
            {
                UseSandbox = true,
                ShopOperatorCode = "9000001",
                LanguageCode = 1,
                CurrencyUiCcode = 242,
            };
            await _settingService.SaveSettingAsync(settings);

            //locales
            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
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

            await base.InstallAsync();

            var task = new ScheduleTask
            {
                Enabled = true,
                Name = "Gestpay Verified Payment Check",
                Seconds = 180,
                StopOnError = false,
                Type = "Nop.Plugin.Payments.GestPay.Helper.RiskifiedStatusCheckScheduler, Nop.Plugin.Payments.GestPay"
            };

            await _scheduleTaskService.InsertTaskAsync(task);
        }

        /// <summary>
        /// Disinstallazione Plugin
        /// </summary>
        public override async Task UninstallAsync()
        {
            //settings
            await _settingService.DeleteSettingAsync<GestPayPaymentSettings>();

            //locales
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.GestPay");

            await base.UninstallAsync();

            var task = await _scheduleTaskService.GetTaskByTypeAsync("Nop.Plugin.Payments.GestPay.Helper.RiskifiedStatusCheckScheduler, Nop.Plugin.Payments.GestPay");
            if (task != null)
                await _scheduleTaskService.DeleteTaskAsync(task);
        }


        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult { NewPaymentStatus = PaymentStatus.Pending };
            return Task.FromResult(result);
        }


        public bool SupportCapture => true;

        public bool SupportPartiallyRefund => true;

        public bool SupportRefund => true;

        public bool SupportVoid => true;

        public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;

        public PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;

        public bool SkipPaymentInfo => false;

        public bool HideInWidgetList => false;

        public Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            return Task.FromResult(new CancelRecurringPaymentResult { Errors = new[] { "Recurring payment not supported" } });
        }

        public Task<bool> CanRePostProcessPaymentAsync(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            //let's ensure that at least 1 minute passed after order is placed
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalMinutes < 1)
                return Task.FromResult(false);

            return Task.FromResult(true);
        }

        public async Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            try
            {
                var client = GetS2SClient();
                var shopTransactionId = capturePaymentRequest.Order.OrderGuid.ToString();
                var bankTransactionId = capturePaymentRequest.Order.AuthorizationTransactionId;
                var amount = capturePaymentRequest.Order.OrderTotal.ToString("0.00", CultureInfo.InvariantCulture);

                var response = await client.callSettleS2SAsync(
                    _gestPayPaymentSettings.ShopOperatorCode,
                    _gestPayPaymentSettings.CurrencyUiCcode.ToString(),
                    amount,
                    shopTransactionId,
                    bankTransactionId,
                    null,null,
                    _gestPayPaymentSettings.ApiKey,
                    null
                );

                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(response.OuterXml);

                var errorCode = GetValueFromXml(xmlDoc, "//ErrorCode");
                var errorDescription = GetValueFromXml(xmlDoc, "//ErrorDescription");

                if (errorCode == "0")
                {
                    result.NewPaymentStatus = PaymentStatus.Paid;
                    result.CaptureTransactionId = GetValueFromXml(xmlDoc, "//BankTransactionID");
                }
                else
                {
                    result.AddError($"Error capturing payment. Code: {errorCode}, Description: {errorDescription}");
                }
            }
            catch (Exception ex)
            {
                result.AddError($"Exception capturing payment: {ex.Message}");
            }

            return result;
        }

        public async Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
        {
            return await _orderTotalCalculationService.CalculatePaymentAdditionalFeeAsync(cart,
                _gestPayPaymentSettings.AdditionalFee, _gestPayPaymentSettings.AdditionalFeePercentage);
        }

        public Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
        {
            return Task.FromResult(new ProcessPaymentRequest());
        }

        public Task<IList<string>> GetWidgetZonesAsync()
        {
            return Task.FromResult<IList<string>>( new List<string> 
            {
                PublicWidgetZones.Footer,
                AdminWidgetZones.OrderDetailsButtons
            });
        }

        public Type GetWidgetViewComponent(string widgetZone)
        {
            if (widgetZone is null)
                throw new ArgumentNullException(nameof(widgetZone));

            if (widgetZone.Equals(PublicWidgetZones.Footer))
                return typeof(GestpayGuaranteedPaymentViewComponent);

            if (widgetZone.Equals(AdminWidgetZones.OrderDetailsButtons))
                return typeof(GestpayPaymentLinkViewComponent);


            return null;
        }


        public Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
        {
            return Task.FromResult(false);
        }

        public async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
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

            var nopBillingAddress = await _addressService.GetAddressByIdAsync(postProcessPaymentRequest.Order.BillingAddressId);

            var amount = Math.Round(postProcessPaymentRequest.Order.OrderTotal, 2);
            var shopTransactionId = postProcessPaymentRequest.Order.OrderGuid.ToString();
            var buyerName = String.Format(
                "{0} {1}",
                nopBillingAddress?.FirstName,
                nopBillingAddress?.LastName
            );

            var endpoint = _gestPayPaymentSettings.UseSandbox ? EndpointConfiguration.WSCryptDecryptSoap12Test : EndpointConfiguration.WSCryptDecryptSoap12;
            var objCryptDecrypt = new WSCryptDecryptSoapClient(endpoint);

            var billingCountry = await _countryService.GetCountryByIdAsync(Convert.ToInt32(nopBillingAddress.CountryId));
            var billingStateProvince = await _stateProvinceService.GetStateProvinceByAddressAsync(nopBillingAddress);

            Address nopShippingAddress = null;
            Country shippingCountry = null;
            StateProvince shippingStateProvince = null;
            if (postProcessPaymentRequest.Order.ShippingAddressId != null)
            {
                nopShippingAddress = await _addressService.GetAddressByIdAsync((int)postProcessPaymentRequest.Order.ShippingAddressId);
                shippingCountry = await _countryService.GetCountryByIdAsync((int)nopShippingAddress.CountryId);
                shippingStateProvince = await _stateProvinceService.GetStateProvinceByAddressAsync(nopShippingAddress);
            }

            //XmlNode xmlResponse;
            var paymentDetails = new GestPayServiceReference.EcommGestpayPaymentDetails();
            if (_gestPayPaymentSettings.EnableGuaranteedPayment)
            {
                var fraudPrevention = new GestPayServiceReference.FraudPrevention();
                fraudPrevention.BeaconSessionID = _httpContextAccessor.HttpContext?.Session.Id;
                fraudPrevention.SubmitForReview = "1";
                fraudPrevention.OrderDateTime = postProcessPaymentRequest.Order.CreatedOnUtc.ToString();
                fraudPrevention.Source = "desktop_web";
                fraudPrevention.SubmissionReason = "rule_decision";
                var currentStore = await _storeContext.GetCurrentStoreAsync();
                fraudPrevention.VendorName = currentStore.Name;
                paymentDetails.FraudPrevention = fraudPrevention;

                //var logger = Nop.Core.Infrastructure.EngineContext.Current.Resolve<Nop.Services.Logging.ILogger>();
                //logger.Information("Gestpay BeaconId = " + _httpContextAccessor.HttpContext.Session.Id);

                var customer = await _customerService.GetCustomerByIdAsync(postProcessPaymentRequest.Order.CustomerId);
                var customerDetail = new GestPayServiceReference.CustomerDetail();
                customerDetail.PrimaryEmail = nopBillingAddress.Email;
                customerDetail.MerchantCustomerID = postProcessPaymentRequest.Order.CustomerId.ToString();
                customerDetail.FirstName = nopBillingAddress.FirstName;
                customerDetail.Lastname = nopBillingAddress.LastName;
                customerDetail.PrimaryPhone = nopBillingAddress.PhoneNumber;
                customerDetail.Company = nopBillingAddress.Company;
                customerDetail.CreatedAtDate = customer?.CreatedOnUtc.ToString();
                customerDetail.VerifiedEmail = "true";
                customerDetail.AccountType = "normal";
                paymentDetails.CustomerDetail = customerDetail;

                if (nopShippingAddress != null)
                {
                    var shippingAddress = new GestPayServiceReference.ShippingAddress();
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

                GestPayServiceReference.BillingAddress billingAddress = new GestPayServiceReference.BillingAddress();
                billingAddress.ProfileID = postProcessPaymentRequest.Order.BillingAddressId.ToString();
                billingAddress.FirstName = nopBillingAddress.FirstName;
                billingAddress.Lastname = nopBillingAddress.LastName;
                billingAddress.StreetName = nopBillingAddress.Address1;
                billingAddress.Streetname2 = nopBillingAddress.Address2;
                billingAddress.City = nopBillingAddress.City;
                billingAddress.ZipCode = nopBillingAddress.ZipPostalCode;
                billingAddress.State = billingStateProvince?.Name;
                billingAddress.CountryCode = billingCountry?.TwoLetterIsoCode;
                billingAddress.Email = nopBillingAddress.Email;
                billingAddress.PrimaryPhone = nopBillingAddress.PhoneNumber;
                billingAddress.Company = nopBillingAddress.Company;
                billingAddress.StateCode = billingStateProvince?.Abbreviation;
                paymentDetails.BillingAddress = billingAddress;

                var orderItems = await _orderService.GetOrderItemsAsync(postProcessPaymentRequest.Order.Id);
                var productDetails = new List<GestPayServiceReference.ProductDetail>();
                decimal itemsTotalInclTax = 0;
                foreach (var item in orderItems)
                {
                    var product = await _productService.GetProductByIdAsync(item.ProductId);

                    if (product != null)
                    {
                        var productDetail = new GestPayServiceReference.ProductDetail();
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
                                GestPayServiceReference.DigitalGiftCardDetails giftcardDetails = new GestPayServiceReference.DigitalGiftCardDetails();
                                var associatedGiftCards = await _giftCardService.GetAllGiftCardsAsync(postProcessPaymentRequest.Order.Id);
                                foreach (var giftcard in associatedGiftCards)
                                {
                                    giftcardDetails.SenderName = giftcard.SenderName;
                                    giftcardDetails.DisplayName = giftcard.SenderName;
                                    giftcardDetails.GreetingMessage = giftcard.Message;

                                    GestPayServiceReference.Recipient recipient = new GestPayServiceReference.Recipient();
                                    recipient.Email = giftcard.RecipientEmail;
                                    giftcardDetails.Recipient = recipient;

                                    break;
                                }
                                productDetail.DigitalGiftCardDetails = giftcardDetails;
                            }
                        }

                        productDetail.Vat = item.PriceInclTax > 0 ? "22" : "0";
                        productDetail.Condition = "new";

                        var productManufacturers = await _manufacturerService.GetProductManufacturersByProductIdAsync(product.Id);
                        productDetail.Brand = (await _manufacturerService.GetManufacturerByIdAsync((int)productManufacturers.FirstOrDefault()?.ManufacturerId))?.Name;
                        //productDetail.DeliveryAt = "home";
                        productDetails.Add(productDetail);

                        itemsTotalInclTax += item.UnitPriceInclTax * item.Quantity;
                    }
                }
                paymentDetails.ProductDetails = productDetails.ToArray();

                IList<GestPayServiceReference.DiscountCode> discountCodes = new List<GestPayServiceReference.DiscountCode>();
                IList<GestPayServiceReference.ShippingLine> shippingLines = new List<GestPayServiceReference.ShippingLine>();

                //  Discount on Sub Total
                var subTotalDiscountCode = new GestPayServiceReference.DiscountCode();
                subTotalDiscountCode.Code = "Order subtotal discount";
                subTotalDiscountCode.Amount = postProcessPaymentRequest.Order.OrderSubTotalDiscountInclTax.ToString("0.00", CultureInfo.InvariantCulture);
                discountCodes.Add(subTotalDiscountCode);

                //  Discount on Total
                var discountCode = new GestPayServiceReference.DiscountCode();
                discountCode.Code = "Order total discount";
                discountCode.Amount = postProcessPaymentRequest.Order.OrderDiscount.ToString("0.00", CultureInfo.InvariantCulture);
                discountCodes.Add(discountCode);

                //  Shipping
                var shipping = new GestPayServiceReference.ShippingLine();
                shipping.Code = postProcessPaymentRequest.Order.ShippingRateComputationMethodSystemName;
                shipping.Title = postProcessPaymentRequest.Order.ShippingRateComputationMethodSystemName;
                shipping.Price = postProcessPaymentRequest.Order.OrderShippingInclTax.ToString("0.00", CultureInfo.InvariantCulture);
                shippingLines.Add(shipping);

                //  Additional Charges / GiftWrap / GiftCard
                var extraAdjustment = postProcessPaymentRequest.Order.OrderTotal - (itemsTotalInclTax + postProcessPaymentRequest.Order.OrderShippingInclTax - postProcessPaymentRequest.Order.OrderDiscount - postProcessPaymentRequest.Order.OrderSubTotalDiscountInclTax);
                if (extraAdjustment > 0)
                {
                    var additionalShipping = new GestPayServiceReference.ShippingLine();
                    additionalShipping.Code = "Additional Charge/Giftwrap";
                    additionalShipping.Title = "Additional Charge/Giftwrap";
                    additionalShipping.Price = extraAdjustment.ToString("0.00", CultureInfo.InvariantCulture);
                    shippingLines.Add(additionalShipping);
                }
                else
                {
                    var additionalDiscount = new GestPayServiceReference.DiscountCode();
                    additionalDiscount.Code = "Giftcard/Additional Discount";
                    additionalDiscount.Amount = (extraAdjustment * -1).ToString("0.00", CultureInfo.InvariantCulture);      // * -1 converts into positive number 
                    discountCodes.Add(additionalDiscount);
                }

                paymentDetails.DiscountCodes = discountCodes.ToArray();
                paymentDetails.ShippingLines = shippingLines.ToArray();
            }

            //  3DS
            var threeDsTransDetails = new ThreeDSEncryptTransDetails();
            threeDsTransDetails.type = "EC";
            threeDsTransDetails.authenticationAmount = amount.ToString("0.00", CultureInfo.InvariantCulture);

            var threeDsContainer = new EncryptThreeDsContainer();
            threeDsContainer.transTypeReq = "P";
            //threeDSContainer.exemption = "SKIP";  As asked by Gestpay Support

            var buyerDetails = new GestPayServiceReference.BuyerDetails();

            var threeDsBillingAddress = new GestPayServiceReference.ThreeDSBillingAddress();
            threeDsBillingAddress.line1 = nopBillingAddress?.Address1;
            threeDsBillingAddress.line2 = nopBillingAddress?.Address2;
            threeDsBillingAddress.city = nopBillingAddress?.City;
            threeDsBillingAddress.postCode = nopBillingAddress?.ZipPostalCode;
            threeDsBillingAddress.state = billingStateProvince?.Name;
            threeDsBillingAddress.country = billingCountry?.TwoLetterIsoCode;
            buyerDetails.billingAddress = threeDsBillingAddress;

            if (nopShippingAddress != null)
            {
                GestPayServiceReference.ThreeDSShippingAddress threeDsShippingAddress = new GestPayServiceReference.ThreeDSShippingAddress();
                threeDsShippingAddress.line1 = nopShippingAddress?.Address1;
                threeDsShippingAddress.line2 = nopShippingAddress?.Address2;
                threeDsShippingAddress.city = nopShippingAddress?.City;
                threeDsShippingAddress.postCode = nopShippingAddress?.ZipPostalCode;
                threeDsShippingAddress.state = shippingStateProvince?.Name;
                threeDsShippingAddress.country = shippingCountry?.TwoLetterIsoCode;
                buyerDetails.shippingAddress = threeDsShippingAddress;
            }

            buyerDetails.addrMatch = "N";

            threeDsContainer.buyerDetails = buyerDetails;
            threeDsTransDetails.threeDsContainer = threeDsContainer;

            var xmlResponse = await objCryptDecrypt.EncryptAsync(
                     _gestPayPaymentSettings.ShopOperatorCode,
                     _gestPayPaymentSettings.CurrencyUiCcode.ToString(),
                     amount.ToString("0.00", CultureInfo.InvariantCulture),
                     shopTransactionId,
                     "", "", "","","",
                     buyerName, nopBillingAddress.Email,
                     _gestPayPaymentSettings.LanguageCode.ToString(), "",
                     "Order Number = " + postProcessPaymentRequest.Order.CustomOrderNumber, 
                     "", "", "",
                     null,
                     null,
                     null, null,
                     null, null, null, null, 
                     null, null, null, null, 
                     null, "", 
                     "", "",
                     "",null,paymentDetails,
                     _gestPayPaymentSettings.ApiKey, threeDsTransDetails);

            XmlDocument xmlReturn = new XmlDocument();
            xmlReturn.LoadXml(xmlResponse.EncryptResult.OuterXml);

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

        public Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            return Task.FromResult( new ProcessPaymentResult { Errors = new[] { "Recurring payment not supported" } });
        }

        public async Task<RefundPaymentResult>  RefundAsync(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            try
            {
                var client = GetS2SClient();
                var shopTransactionId = refundPaymentRequest.Order.OrderGuid.ToString();
                var bankTransactionId = refundPaymentRequest.Order.AuthorizationTransactionId;
                var amount = refundPaymentRequest.AmountToRefund.ToString("0.00", CultureInfo.InvariantCulture);

                var request = new callRefundS2SRequest
                {
                    shopLogin = _gestPayPaymentSettings.ShopOperatorCode,
                    uicCode = _gestPayPaymentSettings.CurrencyUiCcode.ToString(),
                    amount = amount,
                    shopTransactionId = shopTransactionId,
                    bankTransactionId = bankTransactionId,
                    apikey = _gestPayPaymentSettings.ApiKey
                };

                var response = await ((WSs2sSoap)client).callRefundS2SAsync(request);
                var xmlDoc = new XmlDocument();
                
                if (response?.callRefundS2SResult != null)
                {
                    xmlDoc.LoadXml(response.callRefundS2SResult.OuterXml);
                    var errorCode = GetValueFromXml(xmlDoc, "//ErrorCode");
                    var errorDescription = GetValueFromXml(xmlDoc, "//ErrorDescription");

                    if (errorCode == "0")
                    {
                        result.NewPaymentStatus = refundPaymentRequest.IsPartialRefund ? PaymentStatus.PartiallyRefunded : PaymentStatus.Refunded;
                    }
                    else
                    {
                        result.AddError($"Error refunding payment. Code: {errorCode}, Description: {errorDescription}");
                    }
                }
                else
                {
                     result.AddError("Error refunding payment. Empty response from service.");
                }

            }
            catch (Exception ex)
            {
                result.AddError($"Exception refunding payment: {ex.Message}");
            }

            return result;
        }

        public Task<IList<string>>  ValidatePaymentFormAsync(IFormCollection form)
        {
            return Task.FromResult<IList<string>>(new List<string>());
        }

        public async Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            try
            {
                var client = GetS2SClient();
                var shopTransactionId = voidPaymentRequest.Order.OrderGuid.ToString();
                var bankTransactionId = voidPaymentRequest.Order.AuthorizationTransactionId;

                var response = await client.callDeleteS2SAsync(
                    _gestPayPaymentSettings.ShopOperatorCode,
                    shopTransactionId,
                    bankTransactionId,
                    "Void by nopCommerce",
                    _gestPayPaymentSettings.ApiKey,
                    null
                );

                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(response.OuterXml);

                var errorCode = GetValueFromXml(xmlDoc, "//ErrorCode");
                var errorDescription = GetValueFromXml(xmlDoc, "//ErrorDescription");

                if (errorCode == "0")
                {
                    result.NewPaymentStatus = PaymentStatus.Voided;
                }
                else
                {
                    result.AddError($"Error voiding payment. Code: {errorCode}, Description: {errorDescription}");
                }
            }
            catch (Exception ex)
            {
                result.AddError($"Exception voiding payment: {ex.Message}");
            }

            return result;
        }

        #endregion
    }
}