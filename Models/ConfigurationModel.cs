

using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.GestPay.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Payments.GestPay.Fields.UseSandbox")]
        public bool UseSandbox { get; set; }

        [NopResourceDisplayName("Plugins.Payments.GestPay.Fields.UseStarter")]
        public bool UseStarter { get; set; }
        public bool UseSandboxOverrideForStore { get; set; }
        public bool UseStarterOverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.GestPay.Fields.ShopOperatorCode")]
        public string ShopOperatorCode { get; set; }
        public bool ShopOperatorCodeOverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.GestPay.Fields.AdditionalFee")]
        public decimal AdditionalFee { get; set; }
        public bool AdditionalFeeOverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.GestPay.Fields.AdditionalFeePercentage")]
        public bool AdditionalFeePercentage { get; set; }
        public bool AdditionalFeePercentageOverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.GestPay.Fields.CurrencyUICcode")]
        public int CurrencyUiCcode { get; set; }
        public bool CurrencyUiCcodeOverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.GestPay.Fields.LanguageCode")]
        public int LanguageCode { get; set; }
        public bool LanguageCodeOverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.GestPay.Fields.ApiKey")]
        public string ApiKey { get; set; }
        public bool ApiKeyOverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.GestPay.Fields.EnableGuaranteedPayment")]
        public bool EnableGuaranteedPayment { get; set; }
        public bool EnableGuaranteedPaymentOverrideForStore { get; set; }
    }
}