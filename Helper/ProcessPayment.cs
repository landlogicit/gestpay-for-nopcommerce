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
using System.Net.Http;
using System.Threading.Tasks;

namespace Nop.Plugin.Payments.GestPay.Helper
{
    public class ProcessPayment
    {
        private readonly IAddressService _addressService;
        private readonly ILogger _logger;
        private readonly IOrderService _orderService;
        private readonly GestPayPaymentSettings _gestpayPayByLinkPaymentSettings;
        private static readonly HttpClient _httpClient = new HttpClient(); // HttpClient statico

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

        public async Task<int> CreatePaymentAsync(int orderId)
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
            var json = JsonConvert.SerializeObject(model);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
            httpRequest.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            httpRequest.Headers.Add("Authorization", "apikey " + _gestpayPayByLinkPaymentSettings.ApiKey);

            try
            {
                using var response = await _httpClient.SendAsync(httpRequest);
                response.EnsureSuccessStatusCode();
                responseStr = await response.Content.ReadAsStringAsync();

                var paymentResponse = JsonConvert.DeserializeObject<PaymentCreateResponseModel>(responseStr);
                return Convert.ToInt32(paymentResponse.error.code);
            }
            catch (HttpRequestException ex)
            {
                if (ex.Data.Contains("Response"))
                {
                    responseStr = ex.Data["Response"]?.ToString();
                }
                var paymentResponse = !string.IsNullOrEmpty(responseStr)
                    ? JsonConvert.DeserializeObject<PaymentCreateResponseModel>(responseStr)
                    : null;
                await _logger.ErrorAsync("Gestpay Pay Link Error = " + paymentResponse?.error?.code + " " + paymentResponse?.error?.description, ex);
                return -1;
            }
        }
    }
}
