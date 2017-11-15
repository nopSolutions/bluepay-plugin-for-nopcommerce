using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Plugin.Payments.BluePay.Models;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.BluePay.Components
{
    [ViewComponent(Name = "PaymentBluePay")]
    public class PaymentBluePayViewComponent : NopViewComponent
    {
        public IViewComponentResult Invoke()
        {
            var model = new PaymentInfoModel();

            //years
            for (var i = 0; i < 15; i++)
            {
                var year = Convert.ToString(DateTime.Now.Year + i);
                model.ExpireYears.Add(new SelectListItem
                {
                    Text = year,
                    Value = year,
                });
            }

            //months
            for (var i = 1; i <= 12; i++)
            {
                model.ExpireMonths.Add(new SelectListItem
                {
                    Text = i.ToString("D2"),
                    Value = i.ToString()
                });
            }

            //set postback values
            model.CardNumber = Request.Form["CardNumber"];
            model.CardCode = Request.Form["CardCode"];
            var selectedMonth = model.ExpireMonths.FirstOrDefault(x => x.Value.Equals(Request.Form["ExpireMonth"], StringComparison.InvariantCultureIgnoreCase));
            if (selectedMonth != null)
                selectedMonth.Selected = true;
            var selectedYear = model.ExpireYears.FirstOrDefault(x => x.Value.Equals(Request.Form["ExpireYear"], StringComparison.InvariantCultureIgnoreCase));
            if (selectedYear != null)
                selectedYear.Selected = true;

            return View("~/Plugins/Payments.BluePay/Views/PaymentInfo.cshtml", model);
        }
    }
}
