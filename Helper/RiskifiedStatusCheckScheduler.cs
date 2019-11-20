using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Services.Orders;
using Nop.Services.Tasks;
using System.Collections.Generic;
using System.Xml;

namespace Nop.Plugin.Payments.GestPay.Helper
{
    public class RiskifiedStatusCheckScheduler : IScheduleTask
    {
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;

        private readonly GestPayPaymentSettings _gestPayPaymentSettings;

        public RiskifiedStatusCheckScheduler(IOrderService orderService,
            IOrderProcessingService orderProcessingService,
            GestPayPaymentSettings gestPayPaymentSettings)
        {
            _orderService = orderService;
            _orderProcessingService = orderProcessingService;
            _gestPayPaymentSettings = gestPayPaymentSettings;
        }

        public void Execute()
        {
            if (_gestPayPaymentSettings.EnableGuaranteedPayment)
            {
                string errorCode;
                dynamic endpoint;
                dynamic client;
                if (_gestPayPaymentSettings.UseSandbox)
                {
                    endpoint = GestPayWsS2SServiceReferenceTest.WSs2sSoapClient.EndpointConfiguration.WSs2sSoap12;
                    client = new GestPayWsS2SServiceReferenceTest.WSs2sSoapClient(endpoint);
                }
                else
                {
                    endpoint = GestPayWsS2SServiceReference.WSs2sSoapClient.EndpointConfiguration.WSs2sSoap12;
                    client = new GestPayWsS2SServiceReference.WSs2sSoapClient(endpoint);
                }
                

                var orders = _orderService.SearchOrders(osIds: new List<int> { (int)OrderStatus.Pending }, psIds: new List<int> { (int)PaymentStatus.Pending }, paymentMethodSystemName: "Payments.GestPay");

                foreach (var order in orders)
                {
                    if (!string.IsNullOrEmpty(order.AuthorizationTransactionId))
                    {
                        string xmlResponse = client.callReadTrxS2SAsync(_gestPayPaymentSettings.ShopOperatorCode, order.OrderGuid.ToString(), order.AuthorizationTransactionId, _gestPayPaymentSettings.ApiKey, null).Result.OuterXml;
                        
                        XmlDocument xmlReturn = new XmlDocument();
                        xmlReturn.LoadXml(xmlResponse.ToLower());

                        //Id transazione inviato  
                    
                        XmlNode thisNode = xmlReturn.SelectSingleNode("/GestPayS2S/errorcode");
                        errorCode = thisNode.InnerText;
                        //Recupero la Descrizione errore
                       
                        if (errorCode == "0")
                        {
                            thisNode = xmlReturn.SelectSingleNode("/GestPayS2S/risk/RiskResponseCode");

                            var riskifiedCode = thisNode.InnerText;

                            if (riskifiedCode == "approved")
                            {
                                _orderProcessingService.MarkOrderAsPaid(order);
                            }
                        }
                    }
                }
            }
        }
    }
}
