using Nop.Web.Framework;
using Nop.Web.Framework.Mvc;

namespace Nop.Plugin.Payments.GestPay.Models
{
    public class GeneralErrorModel : BaseNopModel
    {
        public string PageMessage { get; set; }

        public string SummaryTitle { get; set; }

        public string SummaryMessage { get; set; }
    }
}