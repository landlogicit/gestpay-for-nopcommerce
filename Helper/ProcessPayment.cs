using Newtonsoft.Json;
using Nop.Plugin.Payments.GestPay.Models.GestpayByLink;
using Nop.Services.Common;
using Nop.Services.Logging;
using Nop.Services.Orders;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Nop.Plugin.Payments.GestPay.Helper
{
    public class ProcessPayment
    {
        private readonly IAddressService _addressService;
        private readonly ILogger _logger;
        private readonly IOrderService _orderService;

        private readonly GestPayPaymentSettings _gestpayPayByLinkPaymentSettings;

        public ProcessPayment(IAddressService addressService,
            ILogger logger,
            IOrderService orderService,
            GestPayPaymentSettings gestpayPayByLinkPaymentSettings)
        {
            _addressService = addressService;
            _logger = logger;
            _orderService = orderService;
            _gestpayPayByLinkPaymentSettings = gestpayPayByLinkPaymentSettings;
        }

        public async Task<int> CreatePayment(int orderId)
        {
            var order = await _orderService.GetOrderByIdAsync(orderId);

            if (order == null)
                throw new ArgumentNullException("order");

            var nopBillingAddress = await _addressService.GetAddressByIdAsync(order.BillingAddressId);

            var amount = Math.Round(order.OrderTotal, 2);
            var shopTransactionId = order.OrderGuid.ToString();
            var buyerName = String.Format(
                "{0} {1}",
                nopBillingAddress?.FirstName,
                nopBillingAddress?.LastName
            );

            var endpoint = _gestpayPayByLinkPaymentSettings.UseSandbox ? "https://sandbox.gestpay.net/api/v1/payment/create/" : "https://ecomms2s.sella.it/api/v1/payment/create/";

            var orderDetails = new OrderDetails();

            var customInfo = new CustomInfo();
            var myDict = new Dictionary<string, string>();
            myDict.Add("OrderNumber", order.CustomOrderNumber);
            customInfo.customInfo = myDict;

            var model = new PaymentCreateRequestModel();
            model.shopLogin = _gestpayPayByLinkPaymentSettings.ShopOperatorCode;
            model.currency = "EUR";
            model.amount = amount.ToString("0.00", CultureInfo.InvariantCulture);
            model.shopTransactionID = shopTransactionId;
            model.buyerName = buyerName;
            model.buyerEmail = nopBillingAddress?.Email;
            model.languageId = _gestpayPayByLinkPaymentSettings.LanguageCode.ToString();

            model.customInfo = customInfo;
            model.orderDetails = orderDetails;

            var paymentChannel = new PaymentChannel();
            paymentChannel.channelType = new List<string> { "EMAIL" };
            model.paymentChannel = paymentChannel;

            var responseStr = string.Empty;
            var request = (HttpWebRequest)WebRequest.Create(endpoint);
            request.ContentType = "application/json";
            request.Headers.Add("Authorization", "apikey " + _gestpayPayByLinkPaymentSettings.ApiKey);
            request.Method = "POST";

            var json = JsonConvert.SerializeObject(model);
            await using (var streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                streamWriter.Write(json);
                streamWriter.Flush();
                streamWriter.Close();
            }

            try
            {
                using HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                var dataStream = response.GetResponseStream();
                var reader = new StreamReader(dataStream);
                responseStr = reader.ReadToEnd();
                reader.Close();
                dataStream.Close();

                var paymentResponse = JsonConvert.DeserializeObject<PaymentCreateResponseModel>(responseStr);
                return Convert.ToInt32(paymentResponse.error.code);
            }
            catch (WebException ex)
            {
                await using (var stream = ex.Response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    responseStr = reader.ReadToEnd();
                }
                var paymentResponse = JsonConvert.DeserializeObject<PaymentCreateResponseModel>(responseStr);
                await _logger.ErrorAsync("Gestpay Pay Link Error = " + paymentResponse.error.code + " " + paymentResponse.error.description, ex);
                return -1;
            }
        }
    }
}
