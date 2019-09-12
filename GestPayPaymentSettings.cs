using Nop.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace Nop.Plugin.Payments.GestPay
{
    public class GestPayPaymentSettings : ISettings
    {
        public bool UseSandbox { get; set; }
        public bool UseStarter { get; set; }
        /// <summary>
        /// Get or sets BancaSella Operator Code 
        /// </summary>
        public string ShopOperatorCode { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether to "additional fee" is specified as percentage. true - percentage, false - fixed value.
        /// </summary>
        public bool AdditionalFeePercentage { get; set; }
        /// <summary>
        /// Additional fee
        /// </summary>
        public decimal AdditionalFee { get; set; }
        /// <summary>
        /// Codice UIC valuta
        /// </summary>
        public int CurrencyUiCcode { get; set; }
        /// <summary>
        /// Codice lingua
        /// </summary>
        public int LanguageCode { get; set; }

        /// <summary>
        /// Api Key
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// Gets or Sets Guaranteed Payment
        /// </summary>
        public bool EnableGuaranteedPayment { get; set; }
    }
}
