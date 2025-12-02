using GestPayWsS2SServiceReference;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Services.Orders;
using System.Collections.Generic;
using Nop.Services.ScheduleTasks;
using Task = System.Threading.Tasks.Task;

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

        public async Task ExecuteAsync()
        {
            if (_gestPayPaymentSettings.EnableGuaranteedPayment)
            {
                string errorCode;
                var endpoint = _gestPayPaymentSettings.UseSandbox ? WSs2sSoapClient.EndpointConfiguration.WSs2sSoap12Test : WSs2sSoapClient.EndpointConfiguration.WSs2sSoap12;
                var client = new WSs2sSoapClient(endpoint);

                var orders = await _orderService.SearchOrdersAsync
                    (osIds: new List<int> { (int)OrderStatus.Pending }, 
                        psIds: new List<int> { (int)PaymentStatus.Pending }, 
                        paymentMethodSystemName: "Payments.GestPay");

                foreach (var order in orders)
                {
                    if (!string.IsNullOrEmpty(order.AuthorizationTransactionId))
                    {
                        var xmlResponse = await client.callReadTrxS2SAsync(
                            _gestPayPaymentSettings.ShopOperatorCode, 
                            order.OrderGuid.ToString(),
                            "",
                            order.AuthorizationTransactionId,
                            apikey:_gestPayPaymentSettings.ApiKey, 
                            null);

                        //  Getting error in below code
                        //XmlDocument xmlReturn = new XmlDocument();
                        //xmlReturn.LoadXml(xmlResponse.ToLower());

                        //Id transazione inviato  
                        errorCode = xmlResponse.SelectSingleNode("ErrorCode")?.InnerText;

                        if (errorCode == "0")
                        {
                            var riskifiedCode = xmlResponse.SelectSingleNode("/RISK/RiskResponseCode")?.InnerText.ToLower();

                            if (riskifiedCode == "approved")
                            {
                                await _orderProcessingService.MarkOrderAsPaidAsync(order);
                            }
                        }
                    }
                }
            }
        }
    }
}
