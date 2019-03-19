using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.GestPay.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Stores;
using Nop.Web.Framework.Controllers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Xml;

namespace Nop.Plugin.Payments.GestPay.Controllers
{
    public class PaymentGestPayController : BasePaymentController
    {
        private readonly IWorkContext _workContext;
        private readonly IStoreService _storeService;
        private readonly ISettingService _settingService;
        private readonly IPaymentService _paymentService;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly ILogger _logger;
        private readonly IWebHelper _webHelper;
        private readonly PaymentSettings _paymentSettings;
        private readonly ILocalizationService _localizationService;
        private readonly GestPayPaymentSettings _gestPayPaymentSettings;

        public PaymentGestPayController(IWorkContext workContext,
            IStoreService storeService,
            ISettingService settingService,
            IPaymentService paymentService,
            IOrderService orderService,
            IOrderProcessingService orderProcessingService,
            ILogger logger, IWebHelper webHelper,
            PaymentSettings paymentSettings,
            ILocalizationService localizationService,
            GestPayPaymentSettings gestPayPaymentSettings)
        {
            _workContext = workContext;
            _storeService = storeService;
            _settingService = settingService;
            _paymentService = paymentService;
            _orderService = orderService;
            _orderProcessingService = orderProcessingService;
            _logger = logger;
            _webHelper = webHelper;
            _paymentSettings = paymentSettings;
            _localizationService = localizationService;
            _gestPayPaymentSettings = gestPayPaymentSettings;
        }

        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            //load settings for a chosen store scope
            var storeScope = GetActiveStoreScopeConfiguration(_storeService, _workContext);
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
                ApiKey = gestPayPaymentSettings.ApiKey,
                ActiveStoreScopeConfiguration = storeScope
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
            }

            return View("~/Plugins/Payments.GestPay/Views/PaymentGestPay/Configure.cshtml", model);
        }

        [HttpPost]
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = GetActiveStoreScopeConfiguration(_storeService, _workContext);
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

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            if (model.UseSandboxOverrideForStore || storeScope == 0)
                _settingService.SaveSetting(gestPayPaymentSettings, x => x.UseSandbox, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(gestPayPaymentSettings, x => x.UseSandbox, storeScope);

            if (model.UseStarterOverrideForStore || storeScope == 0)
                _settingService.SaveSetting(gestPayPaymentSettings, x => x.UseStarter, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(gestPayPaymentSettings, x => x.UseStarter, storeScope);

            if (model.ShopOperatorCodeOverrideForStore || storeScope == 0)
                _settingService.SaveSetting(gestPayPaymentSettings, x => x.ShopOperatorCode, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(gestPayPaymentSettings, x => x.ShopOperatorCode, storeScope);

            if (model.AdditionalFeeOverrideForStore || storeScope == 0)
                _settingService.SaveSetting(gestPayPaymentSettings, x => x.AdditionalFee, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(gestPayPaymentSettings, x => x.AdditionalFee, storeScope);

            if (model.AdditionalFeePercentageOverrideForStore || storeScope == 0)
                _settingService.SaveSetting(gestPayPaymentSettings, x => x.AdditionalFeePercentage, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(gestPayPaymentSettings, x => x.AdditionalFeePercentage, storeScope);

            if (model.LanguageCodeOverrideForStore || storeScope == 0)
                _settingService.SaveSetting(gestPayPaymentSettings, x => x.LanguageCode, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(gestPayPaymentSettings, x => x.LanguageCode, storeScope);

            if (model.CurrencyUiCcodeOverrideForStore || storeScope == 0)
                _settingService.SaveSetting(gestPayPaymentSettings, x => x.CurrencyUiCcode, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(gestPayPaymentSettings, x => x.CurrencyUiCcode, storeScope);

            if (model.ApiKeyOverrideForStore || storeScope == 0)
                _settingService.SaveSetting(gestPayPaymentSettings, x => x.ApiKey, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(gestPayPaymentSettings, x => x.ApiKey, storeScope);

            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }


        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            return View("~/Plugins/Payments.GestPay/Views/PaymentGestPay/PaymentInfo.cshtml");
        }

        [NonAction]
        public override IList<string> ValidatePaymentForm(FormCollection form)
        {
            var warnings = new List<string>();
            return warnings;
        }

        [NonAction]
        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {
            var paymentInfo = new ProcessPaymentRequest();
            return paymentInfo;
        }

        public ActionResult CancelOrder(FormCollection form)
        {
            /* ??Annullare l'ordine o lasciarlo in sospeso come Plugin?? */
            return RedirectToAction("Index", "Home", new { area = "" });
        }

        [ValidateInput(false)]
        public ActionResult s2sHandler()
        {
            string errorCode = "", errorDesc = "";

            string strRequest = Request.QueryString.ToString();
            Dictionary<string, string> values;

            var processor = _paymentService.LoadPaymentMethodBySystemName("Payments.GestPay") as GestPayPaymentProcessor;
            if (processor == null ||
                !processor.IsPaymentMethodActive(_paymentSettings) || !processor.PluginDescriptor.Installed)
                throw new NopException("GestPay module cannot be loaded");

            processor.GetResponseDetails(strRequest, out values);
            if (values != null && values.Count > 0)
            {
                var shopLogin = values["a"];
                var encString = values["b"];
                string shopTransactionId = "", authorizationCode = "", bankTransactionId = "";
                string transactionResult = "", buyerName = "", buyerEmail = "";
                var checkAmount = decimal.Zero;

                var sb = new StringBuilder();
                sb.AppendLine("GestPay s2s:");

                if (processor.IsShopLoginChecked(shopLogin) && encString != null)
                {
                    var objDecrypt = new WSCryptDecrypt(processor.UseSandboxEnvironment());
                    var xmlResponse = objDecrypt.Decrypt(shopLogin, encString, _gestPayPaymentSettings.ApiKey).OuterXml;
                    var xmlReturn = new XmlDocument();
                    xmlReturn.LoadXml(xmlResponse);
                    //Recupero il Codice di errore
                    var thisNode = xmlReturn.SelectSingleNode("/GestPayCryptDecrypt/ErrorCode");
                    errorCode = thisNode.InnerText;
                    //Recupero la Descrizione errore
                    thisNode = xmlReturn.SelectSingleNode("/GestPayCryptDecrypt/ErrorDescription");
                    errorDesc = thisNode.InnerText;
                    //Recupero l'Id transazione (orderId)
                    thisNode = xmlReturn.SelectSingleNode("/GestPayCryptDecrypt/ShopTransactionID");
                    shopTransactionId = thisNode.InnerText;
                    //Recupero il Risultato transazione
                    thisNode = xmlReturn.SelectSingleNode("/GestPayCryptDecrypt/TransactionResult");
                    transactionResult = thisNode.InnerText;

                    //_____ Messaggio OK _____//
                    if (errorCode == "0")
                    {
                        //Recupero il Codice autorizzazione
                        thisNode = xmlReturn.SelectSingleNode("/GestPayCryptDecrypt/AuthorizationCode");
                        authorizationCode = thisNode.InnerText;
                        //Recupero il Codice transazione
                        thisNode = xmlReturn.SelectSingleNode("/GestPayCryptDecrypt/BankTransactionID");
                        bankTransactionId = thisNode.InnerText;
                        //Recupero l'Ammontare della transazione
                        thisNode = xmlReturn.SelectSingleNode("/GestPayCryptDecrypt/Amount");
                        var amount = thisNode.InnerText;
                        //Recupero il Nome dell'utente
                        thisNode = xmlReturn.SelectSingleNode("/GestPayCryptDecrypt/Buyer/BuyerName");
                        buyerName = thisNode.InnerText;
                        //Recupero l'Email utilizzata nella transazione
                        thisNode = xmlReturn.SelectSingleNode("/GestPayCryptDecrypt/Buyer/BuyerEmail");
                        buyerEmail = thisNode.InnerText;
                        //__________ ?validare il totale? __________//
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
                    // Response.Write(Server.HtmlEncode(xmlResponse));
                }

                //________ Inizio composizione messaggio dal server _________//				
                foreach (var kvp in values)
                {
                    sb.AppendLine(kvp.Key + ": " + kvp.Value);
                }
                //Recupero lo stato del pagamento
                var newPaymentStatus = GestPayHelper.GetPaymentStatus(transactionResult, "");
                sb.AppendLine("New payment status: " + newPaymentStatus);
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

                                    order.AuthorizationTransactionId = authorizationCode;
                                    _orderService.UpdateOrder(order);

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
                    order.OrderNotes.Add(new OrderNote()
                    {
                        Note = sb.ToString(),
                        DisplayToCustomer = false,
                        CreatedOnUtc = DateTime.UtcNow
                    });
                    _orderService.UpdateOrder(order);
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

        [ValidateInput(false)]
        public ActionResult EsitoGestPay(string esito = "check")
        {
            //___________ l'aggiornamento è già stato fatto via S2S ___________//
            //byte[] param = Request.BinaryRead(Request.ContentLength);
            //string strRequest = Encoding.ASCII.GetString(param);
            var strRequest = Request.QueryString.ToString();
            Dictionary<string, string> values;

            var processor = _paymentService.LoadPaymentMethodBySystemName("Payments.GestPay") as GestPayPaymentProcessor;
            if (processor == null ||
                !processor.IsPaymentMethodActive(_paymentSettings) || !processor.PluginDescriptor.Installed)
                throw new NopException("GestPay module cannot be loaded");

            processor.GetResponseDetails(strRequest, out values);
            if (values != null && values.Count > 0)
            {
                var shopLogin = values["a"];
                var encString = values["b"];

                if (processor.IsShopLoginChecked(shopLogin) && encString != null)
                {
                    var objDecrypt = new WSCryptDecrypt(processor.UseSandboxEnvironment());
                    var xmlResponse = objDecrypt.Decrypt(shopLogin, encString, _gestPayPaymentSettings.ApiKey).OuterXml;
                    var xmlReturn = new XmlDocument();
                    xmlReturn.LoadXml(xmlResponse);
                    //Codice di errore
                    var thisNode = xmlReturn.SelectSingleNode("/GestPayCryptDecrypt/ErrorCode");
                    string errorCode = thisNode.InnerText;
                    //Descrizione errore
                    thisNode = xmlReturn.SelectSingleNode("/GestPayCryptDecrypt/ErrorDescription");
                    string ErrorDesc = thisNode.InnerText;
                    //Id transazione inviato  
                    thisNode = xmlReturn.SelectSingleNode("/GestPayCryptDecrypt/ShopTransactionID");
                    string shopTransactionID = thisNode.InnerText;

                    //Recupero l'ordine
                    Guid orderNumberGuid = Guid.Empty;
                    try
                    {
                        orderNumberGuid = new Guid(shopTransactionID);
                    }
                    catch { }
                    Order order = _orderService.GetOrderByGuid(orderNumberGuid);

                    if (errorCode == "0" && order != null)
                    {
                        //Codice autorizzazione
                        thisNode = xmlReturn.SelectSingleNode("/GestPayCryptDecrypt/AuthorizationCode");
                        var authorizationCode = thisNode.InnerText;
                        //Codice transazione
                        thisNode = xmlReturn.SelectSingleNode("/GestPayCryptDecrypt/BankTransactionID");
                        var bankTransactionId = thisNode.InnerText;
                        //Ammontare della transazione
                        thisNode = xmlReturn.SelectSingleNode("/GestPayCryptDecrypt/Amount");
                        var amount = thisNode.InnerText;
                        //Risultato transazione
                        thisNode = xmlReturn.SelectSingleNode("/GestPayCryptDecrypt/TransactionResult");
                        var transactionResult = thisNode.InnerText;
                        //Nome dell'utente
                        thisNode = xmlReturn.SelectSingleNode("/GestPayCryptDecrypt/Buyer/BuyerName");
                        var buyerName = thisNode.InnerText;
                        //Email utilizzata nella transazione
                        thisNode = xmlReturn.SelectSingleNode("/GestPayCryptDecrypt/Buyer/BuyerEmail");
                        var buyerEmail = thisNode.InnerText;

                        //load settings for a chosen store scope
                        var storeScope = GetActiveStoreScopeConfiguration(_storeService, _workContext);
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

        [ValidateInput(false)]
        public ActionResult GeneralError()
        {
            var model = new GeneralErrorModel
            {
                PageMessage = "",
                SummaryTitle = "",
                SummaryMessage = ""
            };

            var typErr = Request.QueryString["type"];
            var errC = Request.QueryString["errc"];
            var errD = Request.QueryString["errd"];

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

            return View("~/Plugins/Payments.GestPay/Views/PaymentGestPay/GeneralError.cshtml", model);
        }

    }
}