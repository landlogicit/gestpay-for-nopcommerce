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
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
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
        //private readonly IPaymentService _paymentService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        //private readonly IStoreService _storeService;
        //private readonly IWebHelper _webHelper;
        //private readonly IWorkContext _workContext;

        //private readonly PaymentSettings _paymentSettings;
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
           
            ISettingService settingService,
            IStoreContext storeContext,
           
            
            GestPayPaymentSettings gestPayPaymentSettings)
        {
            _addressService = addressService;
            _localizationService = localizationService;
            _logger = logger;
            _notificationService = notificationService;
            _orderProcessingService = orderProcessingService;
            _orderService = orderService;
            _paymentPluginManager = paymentPluginManager;
            //_paymentService = paymentService;
            _settingService = settingService;
            _storeContext = storeContext;
            //_storeService = storeService;
            //_webHelper = webHelper;
            //_workContext = workContext;
            //_paymentSettings = paymentSettings;
            _gestPayPaymentSettings = gestPayPaymentSettings;
        }
        #endregion

        #region Methods 

        [AuthorizeAdmin]
        [Area(AreaNames.ADMIN)]
        public async Task<IActionResult> Configure()
        {
            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var gestPayPaymentSettings = await _settingService.LoadSettingAsync<GestPayPaymentSettings>(storeScope);

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
                model.UseSandboxOverrideForStore = await _settingService.SettingExistsAsync(gestPayPaymentSettings, x => x.UseSandbox, storeScope);
                model.UseStarterOverrideForStore = await _settingService.SettingExistsAsync(gestPayPaymentSettings, x => x.UseStarter, storeScope);
                model.ShopOperatorCodeOverrideForStore = await _settingService.SettingExistsAsync(gestPayPaymentSettings, x => x.ShopOperatorCode, storeScope);
                model.AdditionalFeeOverrideForStore = await _settingService.SettingExistsAsync(gestPayPaymentSettings, x => x.AdditionalFee, storeScope);
                model.AdditionalFeePercentageOverrideForStore = await _settingService.SettingExistsAsync(gestPayPaymentSettings, x => x.AdditionalFeePercentage, storeScope);
                model.LanguageCodeOverrideForStore = await _settingService.SettingExistsAsync(gestPayPaymentSettings, x => x.LanguageCode, storeScope);
                model.CurrencyUiCcodeOverrideForStore = await _settingService.SettingExistsAsync(gestPayPaymentSettings, x => x.CurrencyUiCcode, storeScope);
                model.ApiKeyOverrideForStore = await _settingService.SettingExistsAsync(gestPayPaymentSettings, x => x.ApiKey, storeScope);
                model.EnableGuaranteedPaymentOverrideForStore = await _settingService.SettingExistsAsync(gestPayPaymentSettings, x => x.EnableGuaranteedPayment, storeScope);
            }

            return View("~/Plugins/Payments.GestPay/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [AutoValidateAntiforgeryToken]
        [Area(AreaNames.ADMIN)]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return await Configure();

            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var gestPayPaymentSettings = await _settingService.LoadSettingAsync<GestPayPaymentSettings>(storeScope);

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
            await _settingService.SaveSettingOverridablePerStoreAsync(gestPayPaymentSettings, x => x.UseSandbox, model.UseSandboxOverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(gestPayPaymentSettings, x => x.UseStarter, model.UseStarterOverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(gestPayPaymentSettings, x => x.ShopOperatorCode, model.ShopOperatorCodeOverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(gestPayPaymentSettings, x => x.AdditionalFee, model.AdditionalFeeOverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(gestPayPaymentSettings, x => x.AdditionalFeePercentage, model.AdditionalFeePercentageOverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(gestPayPaymentSettings, x => x.LanguageCode, model.LanguageCodeOverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(gestPayPaymentSettings, x => x.CurrencyUiCcode, model.CurrencyUiCcodeOverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(gestPayPaymentSettings, x => x.EnableGuaranteedPayment, model.EnableGuaranteedPaymentOverrideForStore, storeScope, false);

            if (model.ApiKeyOverrideForStore || storeScope == 0)
                await _settingService.SaveSettingAsync(gestPayPaymentSettings, x => x.ApiKey, storeScope, false);
            else if (storeScope > 0)
                await _settingService.DeleteSettingAsync(gestPayPaymentSettings, x => x.ApiKey, storeScope);

            //now clear settings cache
            await _settingService.ClearCacheAsync();

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return await Configure();
        }

        [AuthorizeAdmin]
        [Area(AreaNames.ADMIN)]
        [HttpPost]
        public async Task<IActionResult> GeneratePaymentLink(int orderId)
        {
            var order = await _orderService.GetOrderByIdAsync(orderId);
            if (order != null)
            {
                var processPayment = new ProcessPayment(_addressService, _logger, _orderService, _gestPayPaymentSettings);
                var errorCode = await processPayment.CreatePaymentAsync(orderId);

                if (errorCode == 0)
                    return Json("Payment link generated & Email queued");
                return Json("Failed to generate payment link");
            }

            return Json("Order not found");
        }

        public IActionResult CancelOrder(FormCollection form)
        {
            /* ??Annullare l'ordine o lasciarlo in sospeso come Plugin?? */
            return RedirectToAction("Index", "Home", new { area = "" });
        }

        public async Task<IActionResult> S2SHandler()
        {
            string errorCode = "", errorDesc = "";

            var strRequest = Request.QueryString.ToString().Replace("?", "");
            Dictionary<string, string> values;

            var processor = (await _paymentPluginManager.LoadPluginBySystemNameAsync("Payments.GestPay")) as GestPayPaymentProcessor;
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
                string transactionResult = "", buyerName = "", buyerEmail = "", riskified = "", threeDsAuthenticationLevel = "";

                var acceptedThreeDsAuthLevels = new List<string> { "1H", "1F", "2F", "2C", "2E" };
                var checkAmount = decimal.Zero;

                var sb = new StringBuilder();
                sb.AppendLine("GestPay s2s:");

                if (processor.IsShopLoginChecked(shopLogin) && encString != null)
                {
                    var endpoint = _gestPayPaymentSettings.UseSandbox ? WSCryptDecryptSoapClient.EndpointConfiguration.WSCryptDecryptSoap12Test : WSCryptDecryptSoapClient.EndpointConfiguration.WSCryptDecryptSoap12;
                    var objDecrypt = new WSCryptDecryptSoapClient(endpoint);

                    var xmlResponse = (await objDecrypt.DecryptAsync(shopLogin, encString, _gestPayPaymentSettings.ApiKey)).OuterXml;

                    var xmlReturn = new XmlDocument();
                    xmlReturn.LoadXml(xmlResponse.ToLower());

                    //_logger.Information(xmlResponse.ToLower());

                    //Id transazione inviato  
                    errorCode = xmlReturn.SelectSingleNode("/gestpaycryptdecrypt/errorcode")?.InnerText;
                    errorDesc = xmlReturn.SelectSingleNode("/gestpaycryptdecrypt/errordescription")?.InnerText;
                    //authorizationCode = XMLReturn.SelectSingleNode("/gestpaycryptdecrypt/authorizationcode")?.InnerText;
                    shopTransactionId = xmlReturn.SelectSingleNode("/gestpaycryptdecrypt/shoptransactionid")?.InnerText;

                    //_____ Messaggio OK _____//
                    if (errorCode == "0")
                    {
                        //Codice autorizzazione
                        authorizationCode = xmlReturn.SelectSingleNode("/gestpaycryptdecrypt/authorizationcode")?.InnerText;
                        //Codice transazione
                        bankTransactionId = xmlReturn.SelectSingleNode("/gestpaycryptdecrypt/banktransactionid")?.InnerText;
                        //Ammontare della transazione
                        var amount = xmlReturn.SelectSingleNode("/gestpaycryptdecrypt/amount")?.InnerText;
                        //Risultato transazione
                        transactionResult = xmlReturn.SelectSingleNode("/gestpaycryptdecrypt/transactionresult")?.InnerText;
                        //Nome dell'utente
                        buyerName = xmlReturn.SelectSingleNode("/gestpaycryptdecrypt/buyer/buyername")?.InnerText;
                        //Email utilizzata nella transazione
                        buyerEmail = xmlReturn.SelectSingleNode("/gestpaycryptdecrypt/buyer/buyeremail")?.InnerText;

                        //__________ ?validare il totale? __________//
                        riskified = xmlReturn.SelectSingleNode("/gestpaycryptdecrypt/riskresponsedescription")?.InnerText;

                        //  3DS authentication level (1H,1F,2F,2C,2E)
                        threeDsAuthenticationLevel = xmlReturn.SelectSingleNode("/gestpaycryptdecrypt/threeds/authenticationresult/authenticationlevel")?.InnerText.ToUpper();

                        try
                        {
                            if (amount != null) checkAmount = decimal.Parse(amount, new CultureInfo("en-US"));
                        }
                        catch (Exception exc)
                        {
                            await _logger.ErrorAsync("GestPay s2s. Error getting Amount", exc);
                        }
                        sb.AppendLine("GestPay success.");
                    }
                    else
                    {
                        sb.AppendLine("GestPay failed.");
                        await _logger.ErrorAsync("GestPay S2S. Transaction not found", new NopException(sb.ToString()));
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
                sb.AppendLine("3DS Level = " + threeDsAuthenticationLevel);

                //Cerco di recuperare l'ordine
                var orderNumberGuid = Guid.Empty;
                try
                {
                    orderNumberGuid = new Guid(shopTransactionId);
                }
                catch
                {
                    // ignored
                }

                var order = await _orderService.GetOrderByGuidAsync(orderNumberGuid);
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
                                    await _orderProcessingService.MarkAsAuthorizedAsync(order);
                                }
                            }
                            break;
                        case PaymentStatus.Paid:
                            {
                                if (_orderProcessingService.CanMarkOrderAsPaid(order))
                                {
                                    order.AuthorizationTransactionId = bankTransactionId;
                                    order.AuthorizationTransactionCode = authorizationCode;
                                    await _orderService.UpdateOrderAsync(order);

                                    if (!_gestPayPaymentSettings.EnableGuaranteedPayment || acceptedThreeDsAuthLevels.Contains(threeDsAuthenticationLevel))
                                       await _orderProcessingService.MarkOrderAsPaidAsync(order);
                                }
                            }
                            break;
                        case PaymentStatus.Refunded:
                            {
                                if (_orderProcessingService.CanRefundOffline(order))
                                {
                                    await _orderProcessingService.RefundOfflineAsync(order);
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
                                   await _orderProcessingService.CancelOrderAsync(order, true);
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
                    await _orderService.InsertOrderNoteAsync(orderNote);
                }
                else
                {
                    await _logger.ErrorAsync("GestPay S2S. Order is not found", new NopException(sb.ToString()));
                }
            }
            else
            {
                await _logger.ErrorAsync("GestPay S2S failed.", new NopException(strRequest));
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

        public async Task<IActionResult> EsitoGestPay(string esito = "check")
        {
            //___________ l'aggiornamento è già stato fatto via S2S ___________//
            //byte[] param = Request.BinaryRead(Request.ContentLength);
            //string strRequest = Encoding.ASCII.GetString(param);
            var strRequest = Request.QueryString.ToString().Replace("?", "");
            Dictionary<string, string> values;

            var processor = (await _paymentPluginManager.LoadPluginBySystemNameAsync("Payments.GestPay")) as GestPayPaymentProcessor;
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

                    var xmlResponse = (await objDecrypt.DecryptAsync(shopLogin, encString, _gestPayPaymentSettings.ApiKey)).OuterXml;

                    var xmlReturn = new XmlDocument();
                    xmlReturn.LoadXml(xmlResponse.ToLower());

                    //Id transazione inviato  
                    var errorCode = xmlReturn.SelectSingleNode("/gestpaycryptdecrypt/errorcode")?.InnerText;
                    var errorDesc = xmlReturn.SelectSingleNode("/gestpaycryptdecrypt/errordescription")?.InnerText;
                    var shopTransactionId = xmlReturn.SelectSingleNode("/gestpaycryptdecrypt/shoptransactionid")?.InnerText;

                    //Recupero l'ordine
                    var orderNumberGuid = Guid.Empty;
                    try
                    {
                        if (shopTransactionId != null) orderNumberGuid = new Guid(shopTransactionId);
                    }
                    catch
                    {
                        // ignored
                    }

                    var order = await _orderService.GetOrderByGuidAsync(orderNumberGuid);

                    if (errorCode == "0" && order != null)
                    {
                        //Codice autorizzazione
                        //var authorizationCode = xmlReturn.SelectSingleNode("/gestpaycryptdecrypt/authorizationcode")?.InnerText;
                        //Codice transazione
                        //var bankTransactionId = xmlReturn.SelectSingleNode("/gestpaycryptdecrypt/banktransactionid")?.InnerText;
                        //Ammontare della transazione
                        //var amount = xmlReturn.SelectSingleNode("/gestpaycryptdecrypt/amount")?.InnerText;
                        //Risultato transazione
                        //var transactionResult = xmlReturn.SelectSingleNode("/gestpaycryptdecrypt/transactionresult")?.InnerText;
                        //Nome dell'utente
                        //var buyerName = xmlReturn.SelectSingleNode("/gestpaycryptdecrypt/buyer/buyername")?.InnerText;
                        //Email utilizzata nella transazione
                        //var buyerEmail = xmlReturn.SelectSingleNode("/gestpaycryptdecrypt/buyer/buyeremail")?.InnerText;

                        //load settings for a chosen store scope
                        //var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
                        //var gestPayPaymentSettings = await _settingService.LoadSettingAsync<GestPayPaymentSettings>(storeScope);

                        //__________ Ordine Completato __________//
                        return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
                    }
                    else
                    {
                        //__________ ??Comunicarlo all'utente?? __________//
                        return RedirectToAction("GeneralError", new { type = "1", errC = HttpUtility.UrlEncode(errorCode), errD = HttpUtility.UrlEncode(errorDesc) });
                    }
                }
            }
            //________ Pagamento fallito e non posso recuperare i dati ________//
            //throw new NopException("GestPay cannot get transaction parameters.");
            return RedirectToAction("GeneralError", new { type = "2" });
        }

        public async Task<IActionResult> AcceptPaymenyByLinkAsync(string a, string status, string paymentId, string paymentToken)
        {
            var endpoint = _gestPayPaymentSettings.UseSandbox ? "https://sandbox.gestpay.net/api/v1/payment/detail/" + paymentId : "https://ecomms2s.sella.it/api/v1/payment/detail/" + paymentId;

            var responseStr = string.Empty;
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                    request.Headers.Add("Authorization", "apikey " + _gestPayPaymentSettings.ApiKey);
                    request.Headers.Add("paymentToken", paymentToken);

                    using (var response = await httpClient.SendAsync(request))
                    {
                        response.EnsureSuccessStatusCode();
                        responseStr = await response.Content.ReadAsStringAsync();

                        var paymentDetailResponse = JsonConvert.DeserializeObject<PaymentDetailResponseModel>(responseStr);

                        Guid orderGuid;
                        Guid.TryParse(paymentDetailResponse.payload.shopTransactionID, out orderGuid);
                        var order = await _orderService.GetOrderByGuidAsync(orderGuid);

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
                            await _orderService.InsertOrderNoteAsync(orderNote);

                            order.AuthorizationTransactionId = paymentDetailResponse.payload.bankTransactionID;
                            order.AuthorizationTransactionCode = paymentDetailResponse.payload.authorizationCode;

                            await _orderService.UpdateOrderAsync(order);

                            if (!_gestPayPaymentSettings.EnableGuaranteedPayment && paymentDetailResponse.payload.transactionResult == "APPROVED")
                                await _orderProcessingService.MarkOrderAsPaidAsync(order);

                            return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
                        }
                    }
                }
                return Redirect("/");
            }
            catch (HttpRequestException ex)
            {
                // HttpClient exception
                await _logger.ErrorAsync("Gestpay Pay Link Verify HttpRequestException", ex);
                return RedirectToAction("GeneralError", "PaymentGestPay", new { type = "1", errc = "HttpRequestException", errd = HttpUtility.UrlEncode(ex.Message) });
            }
            catch (Exception ex)
            {
                // Try to parse error response if possible
                if (!string.IsNullOrEmpty(responseStr))
                {
                    var paymentDetailResponse = JsonConvert.DeserializeObject<PaymentDetailResponseModel>(responseStr);
                    await _logger.ErrorAsync("Gestpay Pay Link Verify Error = " + paymentDetailResponse?.error?.code + " " + paymentDetailResponse?.error?.description, ex);

                    return RedirectToAction("GeneralError", "PaymentGestPay", new { type = "1", errc = HttpUtility.UrlEncode(paymentDetailResponse?.error?.code), errd = HttpUtility.UrlEncode(paymentDetailResponse?.error?.description) });
                }
                await _logger.ErrorAsync("Gestpay Pay Link Verify Unknown Error", ex);
                return RedirectToAction("GeneralError", "PaymentGestPay", new { type = "1", errc = "Unknown", errd = HttpUtility.UrlEncode(ex.Message) });
            }
        }

        public async Task<IActionResult> GeneralError()
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
                    model.PageMessage = await _localizationService.GetResourceAsync("Plugins.Payments.GestPay.ErrorMessage.PageMessage00");
                    break;
                case "1":
                case "2":
                    model.PageMessage = await _localizationService.GetResourceAsync("Plugins.Payments.GestPay.ErrorMessage.PageMessage01");
                    break;
            }

            if (!string.IsNullOrEmpty(errC) || !String.IsNullOrEmpty(errD))
            {
                model.SummaryTitle = (await _localizationService.GetResourceAsync("Plugins.Payments.GestPay.ErrorMessage.TitleSummary"));
                if (!string.IsNullOrEmpty(errC))
                {
                    model.SummaryMessage += $"Err. Code:{errC}<br/>";
                }
                if (!string.IsNullOrEmpty(errD))
                {
                    model.SummaryMessage += $"Err. Desc:{HttpUtility.UrlDecode(errD)}<br/>";
                }
            }

            return View("~/Plugins/Payments.GestPay/Views/GeneralError.cshtml", model);
        }

        #endregion
    }
}