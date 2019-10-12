using GestPayWsS2SServiceReference;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Services.Orders;
using Nop.Services.Tasks;
using System.Collections.Generic;
using System.Linq;
using static GestPayWsS2SServiceReference.WSs2sSoapClient;

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
                string errorCode, errorDesc;

                var endpoint = _gestPayPaymentSettings.UseSandbox ? EndpointConfiguration.WSs2sSoap12Test : EndpointConfiguration.WSs2sSoap12;
                var client = new WSs2sSoapClient(endpoint);

                var orders = _orderService.SearchOrders(osIds: new List<int> { (int)OrderStatus.Pending }, psIds: new List<int> { (int)PaymentStatus.Pending }, paymentMethodSystemName: "Payments.GestPay");

                foreach (var order in orders)
                {
                    if (!string.IsNullOrEmpty(order.AuthorizationTransactionId))
                    {
                        var xmlResponse = client.callReadTrxS2SAsync(_gestPayPaymentSettings.ShopOperatorCode, order.OrderGuid.ToString(), order.AuthorizationTransactionId, _gestPayPaymentSettings.ApiKey, null).Result;
                        
                        errorCode = xmlResponse.Elements().Where(x => x.Name == "ErrorCode").Single().Value;
                        //Recupero la Descrizione errore
                        errorDesc = xmlResponse.Elements().Where(x => x.Name == "ErrorDescription").Single().Value;

                        if (errorCode == "0")
                        {
                            var riskElement = xmlResponse.Elements().Where(x => x.Name == "RISK").Single();
                            var riskifiedCode = riskElement.Elements().Where(x => x.Name == "RiskResponseCode").Single().Value;

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
