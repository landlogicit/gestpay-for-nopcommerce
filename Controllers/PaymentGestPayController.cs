using GestPayServiceReference;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.GestPay.Helper;
using Nop.Plugin.Payments.GestPay.Models;
using Nop.Plugin.Payments.GestPay.Models.GestpayByLink.PaymentDetails;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Stores;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Xml;

namespace Nop.Plugin.Payments.GestPay.Controllers
{
    public class PaymentGestPayController : BasePaymentController
    {
        #region Fields
        private readonly IAddressService _addressService;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly INotificationService _notificationService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly IPaymentPluginManager _paymentPluginManager;
        private readonly IPaymentService _paymentService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly IStoreService _storeService;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;

        private readonly PaymentSettings _paymentSettings;
        private readonly GestPayPaymentSettings _gestPayPaymentSettings;

        #endregion

        #region Ctor
        public PaymentGestPayController(IAddressService addressService,
            ILocalizationService localizationService,
            ILogger logger,
            INotificationService notificationService,
            IOrderProcessingService orderProcessingService,
            IOrderService orderService,
            IPaymentPluginManager paymentPluginManager,
            IPaymentService paymentService,
            ISettingService settingService,
            IStoreContext storeContext,
            IStoreService storeService,
            IWebHelper webHelper,
            IWorkContext workContext,
            PaymentSettings paymentSettings,
            GestPayPaymentSettings gestPayPaymentSettings)
        {
            _addressService = addressService;
            _localizationService = localizationService;
            _logger = logger;
            _notificationService = notificationService;
            _orderProcessingService = orderProcessingService;
            _orderService = orderService;
            _paymentPluginManager = paymentPluginManager;
            _paymentService = paymentService;
            _settingService = settingService;
            _storeContext = storeContext;
            _storeService = storeService;
            _webHelper = webHelper;
            _workContext = workContext;
            _paymentSettings = paymentSettings;
            _gestPayPaymentSettings = gestPayPaymentSettings;
        }
        #endregion

        #region Methods 

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure()
        {
            //load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var gestPayPaymentSettings = _settingService.LoadSetting<GestPayPaymentSettings>(storeScope);

            var model = new ConfigurationModel
            {
                UseSandbox = gestPayPaymentSettings.UseSandbox,
                UseStarter = gestPayPaymentSettings.UseStarter,
                ShopOperatorCode = gestPayPaymentSettings.ShopOperatorCode,
                AdditionalFee = gestPayPaymentSettings.AdditionalFee,
                AdditionalFeePercentage = gestPayPaymentSettings.AdditionalFeePercentage,
                CurrencyUiCcode = gestPayPaymentSettings.CurrencyUiCcode,
                LanguageCode = gestPayPaymentSettings.LanguageCode,
                ActiveStoreScopeConfiguration = storeScope,
                ApiKey = gestPayPaymentSettings.ApiKey,
                EnableGuaranteedPayment = gestPayPaymentSettings.EnableGuaranteedPayment
            };

            if (storeScope > 0)
            {
                model.UseSandboxOverrideForStore = _settingService.SettingExists(gestPayPaymentSettings, x => x.UseSandbox, storeScope);
                model.UseStarterOverrideForStore = _settingService.SettingExists(gestPayPaymentSettings, x => x.UseStarter, storeScope);
                model.ShopOperatorCodeOverrideForStore = _settingService.SettingExists(gestPayPaymentSettings, x => x.ShopOperatorCode, storeScope);
                model.AdditionalFeeOverrideForStore = _settingService.SettingExists(gestPayPaymentSettings, x => x.AdditionalFee, storeScope);
                model.AdditionalFeePercentageOverrideForStore = _settingService.SettingExists(gestPayPaymentSettings, x => x.AdditionalFeePercentage, storeScope);
                model.LanguageCodeOverrideForStore = _settingService.SettingExists(gestPayPaymentSettings, x => x.LanguageCode, storeScope);
                model.CurrencyUiCcodeOverrideForStore = _settingService.SettingExists(gestPayPaymentSettings, x => x.CurrencyUiCcode, storeScope);
                model.ApiKeyOverrideForStore = _settingService.SettingExists(gestPayPaymentSettings, x => x.ApiKey, storeScope);
                model.EnableGuaranteedPaymentOverrideForStore = _settingService.SettingExists(gestPayPaymentSettings, x => x.EnableGuaranteedPayment, storeScope);
            }

            return View("~/Plugins/Payments.GestPay/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [AutoValidateAntiforgeryToken]
        [Area(AreaNames.Admin)]
        public IActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var gestPayPaymentSettings = _settingService.LoadSetting<GestPayPaymentSettings>(storeScope);

            //save settings
            gestPayPaymentSettings.UseSandbox = model.UseSandbox;
            gestPayPaymentSettings.UseStarter = model.UseStarter;
            gestPayPaymentSettings.ShopOperatorCode = model.ShopOperatorCode;
            gestPayPaymentSettings.AdditionalFee = model.AdditionalFee;
            gestPayPaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;
            gestPayPaymentSettings.LanguageCode = model.LanguageCode;
            gestPayPaymentSettings.CurrencyUiCcode = model.CurrencyUiCcode;
            gestPayPaymentSettings.ApiKey = model.ApiKey;
            gestPayPaymentSettings.EnableGuaranteedPayment = model.EnableGuaranteedPayment;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            _settingService.SaveSettingOverridablePerStore(gestPayPaymentSettings, x => x.UseSandbox, model.UseSandboxOverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(gestPayPaymentSettings, x => x.UseStarter, model.UseStarterOverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(gestPayPaymentSettings, x => x.ShopOperatorCode, model.ShopOperatorCodeOverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(gestPayPaymentSettings, x => x.AdditionalFee, model.AdditionalFeeOverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(gestPayPaymentSettings, x => x.AdditionalFeePercentage, model.AdditionalFeePercentageOverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(gestPayPaymentSettings, x => x.LanguageCode, model.LanguageCodeOverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(gestPayPaymentSettings, x => x.CurrencyUiCcode, model.CurrencyUiCcodeOverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(gestPayPaymentSettings, x => x.EnableGuaranteedPayment, model.EnableGuaranteedPaymentOverrideForStore, storeScope, false);

            if (model.ApiKeyOverrideForStore || storeScope == 0)
                _settingService.SaveSetting(gestPayPaymentSettings, x => x.ApiKey, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(gestPayPaymentSettings, x => x.ApiKey, storeScope);

            //now clear settings cache
            _settingService.ClearCache();

            _notificationService.SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        [HttpPost]
        public IActionResult GeneratePaymentLink(int orderId)
        {
            var order = _orderService.GetOrderById(orderId);
            if (order != null)
            {
                ProcessPayment processPayment = new ProcessPayment(_addressService, _logger, _orderService, _gestPayPaymentSettings);
                var errorCode = processPayment.CreatePayment(orderId);

                if (errorCode == 0)
                    return Json("Payment link generated & Email queued");
                else
                    return Json("Failed to generate payment link");
            }
            else
                return Json("Order not found");
        }

        public IActionResult CancelOrder(FormCollection form)
        {
            /* ??Annullare l'ordine o lasciarlo in sospeso come Plugin?? */
            return RedirectToAction("Index", "Home", new { area = "" });
        }

        public IActionResult s2sHandler()
        {
            string errorCode = "", errorDesc = "";

            string strRequest = Request.QueryString.ToString().Replace("?", "");
            Dictionary<string, string> values;

            var processor = _paymentPluginManager.LoadPluginBySystemName("Payments.GestPay") as GestPayPaymentProcessor;
            if (processor == null ||
                !_paymentPluginManager.IsPluginActive(processor))
                throw new NopException("GestPay module cannot be loaded");

            processor.GetResponseDetails(strRequest, out values);
            if (values != null && values.Count > 0)
            {
                if (values.Count == 4)
                {
                    return RedirectToRoute("Plugin.Payments.GestPay.AcceptPaymenyByLink", new { a = values["a"], status = values["Status"], paymentId = values["paymentID"], paymentToken = values["paymentToken"] });
                }

                var shopLogin = values["a"];
                var encString = values["b"];
                string shopTransactionId = "", authorizationCode = "", bankTransactionId = "";
                string transactionResult = "", buyerName = "", buyerEmail = "", riskified = "", authorizationcode = "", threeDSAuthenticationLevel = "";

                var acceptedThreeDSAuthLevels = new List<string> { "1H", "1F", "2F", "2C", "2E" };
                var checkAmount = decimal.Zero;

                var sb = new StringBuilder();
                sb.AppendLine("GestPay s2s:");

                if (processor.IsShopLoginChecked(shopLogin) && encString != null)
                {
                    var endpoint = _gestPayPaymentSettings.UseSandbox ? WSCryptDecryptSoapClient.EndpointConfiguration.WSCryptDecryptSoap12Test : WSCryptDecryptSoapClient.EndpointConfiguration.WSCryptDecryptSoap12;
                    var objDecrypt = new WSCryptDecryptSoapClient(endpoint);

                    string xmlResponse = objDecrypt.DecryptAsync(shopLogin, encString, _gestPayPaymentSettings.ApiKey).Result.OuterXml;

                    XmlDocument XMLReturn = new XmlDocument();
                    XMLReturn.LoadXml(xmlResponse.ToLower());

                    //_logger.Information(xmlResponse.ToLower());

                    //Id transazione inviato  
                    errorCode = XMLReturn.SelectSingleNode("/gestpaycryptdecrypt/errorcode")?.InnerText;
                    errorDesc = XMLReturn.SelectSingleNode("/gestpaycryptdecrypt/errordescription")?.InnerText;
                    //authorizationCode = XMLReturn.SelectSingleNode("/gestpaycryptdecrypt/authorizationcode")?.InnerText;
                    shopTransactionId = XMLReturn.SelectSingleNode("/gestpaycryptdecrypt/shoptransactionid")?.InnerText;

                    //_____ Messaggio OK _____//
                    if (errorCode == "0")
                    {
                        //Codice autorizzazione
                        authorizationCode = XMLReturn.SelectSingleNode("/gestpaycryptdecrypt/authorizationcode")?.InnerText;
                        //Codice transazione
                        bankTransactionId = XMLReturn.SelectSingleNode("/gestpaycryptdecrypt/banktransactionid")?.InnerText;
                        //Ammontare della transazione
                        var amount = XMLReturn.SelectSingleNode("/gestpaycryptdecrypt/amount")?.InnerText;
                        //Risultato transazione
                        transactionResult = XMLReturn.SelectSingleNode("/gestpaycryptdecrypt/transactionresult")?.InnerText;
                        //Nome dell'utente
                        buyerName = XMLReturn.SelectSingleNode("/gestpaycryptdecrypt/buyer/buyername")?.InnerText;
                        //Email utilizzata nella transazione
                        buyerEmail = XMLReturn.SelectSingleNode("/gestpaycryptdecrypt/buyer/buyeremail")?.InnerText;

                        //__________ ?validare il totale? __________//
                        riskified = XMLReturn.SelectSingleNode("/gestpaycryptdecrypt/riskresponsedescription")?.InnerText;

                        //  3DS authentication level (1H,1F,2F,2C,2E)
                        threeDSAuthenticationLevel = XMLReturn.SelectSingleNode("/gestpaycryptdecrypt/threeds/authenticationresult/authenticationlevel")?.InnerText?.ToUpper();

                        try
                        {
                            checkAmount = decimal.Parse(amount, new CultureInfo("en-US"));
                        }
                        catch (Exception exc)
                        {
                            _logger.Error("GestPay s2s. Error getting Amount", exc);
                        }
                        sb.AppendLine("GestPay success.");
                    }
                    else
                    {
                        sb.AppendLine("GestPay failed.");
                        _logger.Error("GestPay S2S. Transaction not found", new NopException(sb.ToString()));
                    }
                }

                //________ Inizio composizione messaggio dal server _________//				
                foreach (var kvp in values)
                {
                    sb.AppendLine(kvp.Key + ": " + kvp.Value);
                }

                //Recupero lo stato del pagamento
                var newPaymentStatus = GestPayHelper.GetPaymentStatus(transactionResult, "");
                sb.AppendLine("New payment status: " + newPaymentStatus);
                sb.AppendLine("Riskified = " + riskified);
                sb.AppendLine("3DS Level = " + threeDSAuthenticationLevel);

                //Cerco di recuperare l'ordine
                var orderNumberGuid = Guid.Empty;
                try
                {
                    orderNumberGuid = new Guid(shopTransactionId);
                }
                catch { }

                var order = _orderService.GetOrderByGuid(orderNumberGuid);
                //_________ aggiorno lo stato dell'ordine _________//
                if (order != null)
                {
                    switch (newPaymentStatus)
                    {
                        case PaymentStatus.Pending:
                            {
                            }
                            break;
                        case PaymentStatus.Authorized:
                            {
                                if (_orderProcessingService.CanMarkOrderAsAuthorized(order))
                                {
                                    _orderProcessingService.MarkAsAuthorized(order);
                                }
                            }
                            break;
                        case PaymentStatus.Paid:
                            {
                                if (_orderProcessingService.CanMarkOrderAsPaid(order))
                                {
                                    order.AuthorizationTransactionId = bankTransactionId;
                                    order.AuthorizationTransactionCode = authorizationCode;
                                    _orderService.UpdateOrder(order);

                                    if (!_gestPayPaymentSettings.EnableGuaranteedPayment || acceptedThreeDSAuthLevels.Contains(threeDSAuthenticationLevel))
                                        _orderProcessingService.MarkOrderAsPaid(order);
                                }
                            }
                            break;
                        case PaymentStatus.Refunded:
                            {
                                if (_orderProcessingService.CanRefundOffline(order))
                                {
                                    _orderProcessingService.RefundOffline(order);
                                }
                            }
                            break;
                        case PaymentStatus.Voided:
                            {
                                /*_ Visto che non si può impostare il pagamento ad Annullato 
                                 * _orderProcessingService.CanVoidOffline allora cancello l'ordine.
                                 * C'è da decidere se avvisare o meno l'utente _*/
                                if (_orderProcessingService.CanCancelOrder(order))
                                {
                                    _orderProcessingService.CancelOrder(order, true);
                                }
                            }
                            break;
                    }

                    //__________________ salvo i valori restituiti __________________//
                    sb.AppendLine("GestPay response:");
                    //Codice Errore
                    sb.AppendLine("ErrorCode: " + errorCode);
                    //Descrizione Errore
                    sb.AppendLine("ErrorDesc: " + errorDesc);
                    sb.AppendLine("TrxResult: " + transactionResult);
                    sb.AppendLine("BankTrxID: " + bankTransactionId);
                    sb.AppendLine("AuthCode: " + authorizationCode);
                    sb.AppendLine("Amount: " + checkAmount);
                    if (!Math.Round(checkAmount, 2).Equals(Math.Round(order.OrderTotal, 2)))
                    {
                        //__________ ?validare il totale? __________//
                        sb.AppendLine(String.Format("Amount difference: {0}-{1}", Math.Round(checkAmount, 2), Math.Round(order.OrderTotal, 2)));
                    }
                    sb.AppendLine("BuyerName: " + buyerName);
                    sb.AppendLine("BuyerEmail: " + buyerEmail);

                    //Inserisco la nota sull'ordine 
                    var orderNote = new OrderNote
                    {
                        OrderId = order.Id,
                        Note = sb.ToString(),
                        DisplayToCustomer = false,
                        CreatedOnUtc = DateTime.UtcNow
                    };
                    _orderService.InsertOrderNote(orderNote);
                }
                else
                {
                    _logger.Error("GestPay S2S. Order is not found", new NopException(sb.ToString()));
                }
            }
            else
            {
                _logger.Error("GestPay S2S failed.", new NopException(strRequest));
            }

            //_________ Imposto il risultato __________//
            var s2SResponse = "KO";
            if (errorCode == "0")
            {
                s2SResponse = "OK";
            }
            //nothing should be rendered to visitor
            return Content(String.Format("<html>{0}</html>", s2SResponse));
        }

        public IActionResult EsitoGestPay(string esito = "check")
        {
            //___________ l'aggiornamento è già stato fatto via S2S ___________//
            //byte[] param = Request.BinaryRead(Request.ContentLength);
            //string strRequest = Encoding.ASCII.GetString(param);
            var strRequest = Request.QueryString.ToString().Replace("?", "");
            Dictionary<string, string> values;

            var processor = _paymentPluginManager.LoadPluginBySystemName("Payments.GestPay") as GestPayPaymentProcessor;
            if (processor == null ||
                !_paymentPluginManager.IsPluginActive(processor))
                throw new NopException("GestPay module cannot be loaded");

            processor.GetResponseDetails(strRequest, out values);
            if (values != null && values.Count > 0)
            {
                if (values.Count == 4)
                {
                    return RedirectToRoute("Plugin.Payments.GestPay.AcceptPaymenyByLink", new { a = values["a"], status = values["Status"], paymentId = values["paymentID"], paymentToken = values["paymentToken"] });
                }

                var shopLogin = values["a"];
                var encString = values["b"];

                if (processor.IsShopLoginChecked(shopLogin) && encString != null)
                {
                    var endpoint = _gestPayPaymentSettings.UseSandbox ? WSCryptDecryptSoapClient.EndpointConfiguration.WSCryptDecryptSoap12Test : WSCryptDecryptSoapClient.EndpointConfiguration.WSCryptDecryptSoap12;
                    var objDecrypt = new WSCryptDecryptSoapClient(endpoint);

                    string xmlResponse = objDecrypt.DecryptAsync(shopLogin, encString, _gestPayPaymentSettings.ApiKey).Result.OuterXml;

                    XmlDocument XMLReturn = new XmlDocument();
                    XMLReturn.LoadXml(xmlResponse.ToLower());

                    //Id transazione inviato  
                    string errorCode = XMLReturn.SelectSingleNode("/gestpaycryptdecrypt/errorcode")?.InnerText;
                    string ErrorDesc = XMLReturn.SelectSingleNode("/gestpaycryptdecrypt/errordescription")?.InnerText;
                    string shopTransactionId = XMLReturn.SelectSingleNode("/gestpaycryptdecrypt/shoptransactionid")?.InnerText;

                    //Recupero l'ordine
                    Guid orderNumberGuid = Guid.Empty;
                    try
                    {
                        orderNumberGuid = new Guid(shopTransactionId);
                    }
                    catch { }
                    Order order = _orderService.GetOrderByGuid(orderNumberGuid);

                    if (errorCode == "0" && order != null)
                    {
                        //Codice autorizzazione
                        var authorizationCode = XMLReturn.SelectSingleNode("/gestpaycryptdecrypt/authorizationcode")?.InnerText;
                        //Codice transazione
                        var bankTransactionId = XMLReturn.SelectSingleNode("/gestpaycryptdecrypt/banktransactionid")?.InnerText;
                        //Ammontare della transazione
                        var amount = XMLReturn.SelectSingleNode("/gestpaycryptdecrypt/amount")?.InnerText;
                        //Risultato transazione
                        var transactionResult = XMLReturn.SelectSingleNode("/gestpaycryptdecrypt/transactionresult")?.InnerText;
                        //Nome dell'utente
                        var buyerName = XMLReturn.SelectSingleNode("/gestpaycryptdecrypt/buyer/buyername")?.InnerText;
                        //Email utilizzata nella transazione
                        var buyerEmail = XMLReturn.SelectSingleNode("/gestpaycryptdecrypt/buyer/buyeremail")?.InnerText;

                        //load settings for a chosen store scope
                        var storeScope = _storeContext.ActiveStoreScopeConfiguration;
                        var gestPayPaymentSettings = _settingService.LoadSetting<GestPayPaymentSettings>(storeScope);

                        //__________ Ordine Completato __________//
                        return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
                    }
                    else
                    {
                        //__________ ??Comunicarlo all'utente?? __________//
                        return RedirectToAction("GeneralError", new { type = "1", errC = HttpUtility.UrlEncode(errorCode), errD = HttpUtility.UrlEncode(ErrorDesc) });
                    }
                }
            }
            //________ Pagamento fallito e non posso recuperare i dati ________//
            //throw new NopException("GestPay cannot get transaction parameters.");
            return RedirectToAction("GeneralError", new { type = "2" });
        }

        public IActionResult AcceptPaymenyByLink(string a, string status, string paymentId, string paymentToken)
        {
            var endpoint = _gestPayPaymentSettings.UseSandbox ? "https://sandbox.gestpay.net/api/v1/payment/detail/" + paymentId : "https://ecomms2s.sella.it/api/v1/payment/detail/" + paymentId;

            var responseStr = string.Empty;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(endpoint);
            request.ContentType = "application/json";
            request.Headers.Add("Authorization", "apikey " + _gestPayPaymentSettings.ApiKey);
            request.Headers.Add("paymentToken", paymentToken);

            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    Stream dataStream = response.GetResponseStream();
                    StreamReader reader = new StreamReader(dataStream);
                    responseStr = reader.ReadToEnd();
                    reader.Close();
                    dataStream.Close();

                    PaymentDetailResponseModel paymentDetailResponse = JsonConvert.DeserializeObject<PaymentDetailResponseModel>(responseStr);

                    Guid orderGuid;
                    Guid.TryParse(paymentDetailResponse.payload.shopTransactionID, out orderGuid);
                    var order = _orderService.GetOrderByGuid(orderGuid);

                    if (order != null)
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("GestPay s2s:");
                        sb.AppendLine("Res = " + paymentDetailResponse.payload.risk.riskResponseDescription);
                        sb.AppendLine("GestPay response:");
                        sb.AppendLine("ErrorCode: " + paymentDetailResponse.error.code);
                        sb.AppendLine("ErrorDesc: " + paymentDetailResponse.error.description);
                        sb.AppendLine("TrxResult: " + paymentDetailResponse.payload.transactionResult);
                        sb.AppendLine("BankTrxID: " + paymentDetailResponse.payload.bankTransactionID);
                        sb.AppendLine("AuthCode: " + paymentDetailResponse.payload.authorizationCode);
                        sb.AppendLine("Amount: " + paymentDetailResponse.payload.automaticOperation.amount);

                        var amount = Convert.ToDecimal(paymentDetailResponse.payload.automaticOperation.amount);
                        if (!Math.Round(amount, 2).Equals(Math.Round(order.OrderTotal, 2)))
                            sb.AppendLine(String.Format("Amount difference: {0}-{1}", Math.Round(amount, 2), Math.Round(order.OrderTotal, 2)));

                        sb.AppendLine("BuyerName: " + paymentDetailResponse.payload.buyer.name);
                        sb.AppendLine("BuyerEmail: " + paymentDetailResponse.payload.buyer.email);

                        // Inserisco la nota sull'ordine 
                        var orderNote = new OrderNote
                        {
                            OrderId = order.Id,
                            Note = sb.ToString(),
                            DisplayToCustomer = false,
                            CreatedOnUtc = DateTime.UtcNow
                        };
                        _orderService.InsertOrderNote(orderNote);

                        order.AuthorizationTransactionId = paymentDetailResponse.payload.bankTransactionID;
                        order.AuthorizationTransactionCode = paymentDetailResponse.payload.authorizationCode;

                        _orderService.UpdateOrder(order);

                        if (!_gestPayPaymentSettings.EnableGuaranteedPayment && paymentDetailResponse.payload.transactionResult == "APPROVED")
                            _orderProcessingService.MarkOrderAsPaid(order);

                        return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
                    }
                }
                return Redirect("/");
            }
            catch (WebException ex)
            {
                using (var stream = ex.Response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    responseStr = reader.ReadToEnd();
                }
                PaymentDetailResponseModel paymentDetailResponse = JsonConvert.DeserializeObject<PaymentDetailResponseModel>(responseStr);
                _logger.Error("Gestpay Pay Link Verify Error = " + paymentDetailResponse.error.code + " " + paymentDetailResponse.error.description, ex);

                return RedirectToAction("GeneralError", "PaymentGestPay", new { type = "1", errc = HttpUtility.UrlEncode(paymentDetailResponse.error.code), errd = HttpUtility.UrlEncode(paymentDetailResponse.error.description) });
            }
        }

        public IActionResult GeneralError()
        {
            var model = new GeneralErrorModel
            {
                PageMessage = "",
                SummaryTitle = "",
                SummaryMessage = ""
            };

            var typErr = Request.Query["type"];
            var errC = Request.Query["errc"];
            var errD = Request.Query["errd"];

            switch (typErr)
            {
                case "0":
                    model.PageMessage = _localizationService.GetLocaleStringResourceByName("Plugins.Payments.GestPay.ErrorMessage.PageMessage00").ResourceValue;
                    break;
                case "1":
                case "2":
                    model.PageMessage = _localizationService.GetLocaleStringResourceByName("Plugins.Payments.GestPay.ErrorMessage.PageMessage01").ResourceValue;
                    break;
            }

            if (!String.IsNullOrEmpty(errC) || !String.IsNullOrEmpty(errD))
            {
                model.SummaryTitle = _localizationService.GetLocaleStringResourceByName("Plugins.Payments.GestPay.ErrorMessage.TitleSummary").ResourceValue;
                if (!String.IsNullOrEmpty(errC))
                {
                    model.SummaryMessage += String.Format("Err. Code:{0}<br/>", errC);
                }
                if (!String.IsNullOrEmpty(errD))
                {
                    model.SummaryMessage += String.Format("Err. Desc:{0}<br/>", HttpUtility.UrlDecode(errD));
                }
            }

            return View("~/Plugins/Payments.GestPay/Views/GeneralError.cshtml", model);
        }

        #endregion
    }
}