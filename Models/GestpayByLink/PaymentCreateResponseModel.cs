using System;
using System.Collections.Generic;
using System.Text;

namespace Nop.Plugin.Payments.GestPay.Models.GestpayByLink
{
    public class PaymentCreateResponseModel
    {
        public Error error { get; set; }
        public Payload payload { get; set; }
    }

    public class Error
    {
        public string code { get; set; }
        public string description { get; set; }
    }

    public class UserRedirect
    {
        public string href { get; set; }
    }

    public class Payload
    {
        public string paymentToken { get; set; }
        public string paymentID { get; set; }
        public UserRedirect userRedirect { get; set; }
    }
    
}
