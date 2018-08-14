using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;
using System.Collections.Generic;

namespace Nop.Plugin.Payments.BluePay.Models
{
    public class PaymentInfoModel : BaseNopModel
    {
        public PaymentInfoModel()
        {
            ExpireMonths = new List<SelectListItem>();
            ExpireYears = new List<SelectListItem>();
        }

        [NopResourceDisplayName("Payment.CardNumber")]
        public string CardNumber { get; set; }

        [NopResourceDisplayName("Payment.ExpirationDate")]
        public string ExpireMonth { get; set; }

        [NopResourceDisplayName("Payment.ExpirationDate")]
        public string ExpireYear { get; set; }

        public IList<SelectListItem> ExpireMonths { get; set; }
        public IList<SelectListItem> ExpireYears { get; set; }

        [NopResourceDisplayName("Payment.CardCode")]
        public string CardCode { get; set; }
    }
}