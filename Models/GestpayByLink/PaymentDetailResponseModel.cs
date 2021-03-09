using System.Collections.Generic;

namespace Nop.Plugin.Payments.GestPay.Models.GestpayByLink.PaymentDetails
{
    public class PaymentDetailResponseModel
    {
        public Error error { get; set; }
        public Payload payload { get; set; }
    }

    public class Error
    {
        public string code { get; set; }
        public string description { get; set; }
    }

    public class Event2
    {
        public string eventtype { get; set; }
        public string eventamount { get; set; }
        public string eventdate { get; set; }
        public string eventARN { get; set; }
        public string eventID { get; set; }
    }

    public class Event
    {
        public Event2 @event { get; set; }
    }

    public class Buyer
    {
        public string name { get; set; }
        public string email { get; set; }
    }

    public class Risk
    {
        public string riskResponseCode { get; set; }
        public string riskResponseDescription { get; set; }
    }

    public class CustomInfo
    {
        public Dictionary<string, string> customInfo { get; set; }
    }

    public class Dcc
    {
        public string eligible { get; set; }
        public string currency { get; set; }
    }

    public class Vbv
    {
        public string flag { get; set; }
        public string buyer { get; set; }
    }

    public class FraudPrevention
    {
        public string check { get; set; }
        public string state { get; set; }
        public string description { get; set; }
        public string order { get; set; }
    }

    public class AutomaticOperation
    {
        public string type { get; set; }
        public string date { get; set; }
        public string amount { get; set; }
    }

    public class Payload
    {
        public string transactionType { get; set; }
        public string transactionResult { get; set; }
        public string transactionState { get; set; }
        public string transactionErrorCode { get; set; }
        public string transactionErrorDescription { get; set; }
        public string bankTransactionID { get; set; }
        public string shopTransactionID { get; set; }
        public string authorizationCode { get; set; }
        public string paymentID { get; set; }
        public string currency { get; set; }
        public string country { get; set; }
        public string company { get; set; }
        public string tdLevel { get; set; }
        public List<Event> events { get; set; }
        public Buyer buyer { get; set; }
        public Risk risk { get; set; }
        public CustomInfo customInfo { get; set; }
        public string alertCode { get; set; }
        public string alertDescription { get; set; }
        public string cvvPresent { get; set; }
        public Dcc dcc { get; set; }
        public string maskedPAN { get; set; }
        public string paymentMethod { get; set; }
        public string productType { get; set; }
        public string token { get; set; }
        public string tokenExpiryMonth { get; set; }
        public string tokenExpiryYear { get; set; }
        public Vbv vbv { get; set; }
        public string payPalFee { get; set; }
        public FraudPrevention fraudPrevention { get; set; }
        public AutomaticOperation automaticOperation { get; set; }
    }

}
