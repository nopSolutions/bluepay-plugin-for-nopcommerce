using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using Nop.Core;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.BluePay.Models;
using Nop.Plugin.Payments.BluePay.Validators;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Stores;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Payments.BluePay.Controllers
{
    public class PaymentBluePayController : BasePaymentController
    {
        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly ISettingService _settingService;
        private readonly IStoreService _storeService;
        private readonly IWorkContext _workContext;
        
        public PaymentBluePayController(ILocalizationService localizationService,
            ILogger logger,
            IOrderProcessingService orderProcessingService,
            IOrderService orderService,
            ISettingService settingService,
            IStoreService storeService,
            IWorkContext workContext)
        {
            this._localizationService = localizationService;
            this._logger = logger;
            this._orderProcessingService = orderProcessingService;
            this._orderService = orderService;
            this._settingService = settingService;
            this._storeService = storeService;
            this._workContext = workContext;
        }

        [NonAction]
        public override IList<string> ValidatePaymentForm(FormCollection form)
        {
            var warnings = new List<string>();

            //validate
            var validator = new PaymentInfoValidator(_localizationService);
            var model = new PaymentInfoModel
            {
                CardNumber = form["CardNumber"],
                ExpireMonth = form["ExpireMonth"],
                ExpireYear = form["ExpireYear"],
                CardCode = form["CardCode"]
            };
            var validationResult = validator.Validate(model);
            if (!validationResult.IsValid)
                foreach (var error in validationResult.Errors)
                    warnings.Add(error.ErrorMessage);
            return warnings;
        }

        [NonAction]
        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {
            var paymentInfo = new ProcessPaymentRequest();
            paymentInfo.CreditCardNumber = form["CardNumber"];
            paymentInfo.CreditCardExpireMonth = int.Parse(form["ExpireMonth"]);
            paymentInfo.CreditCardExpireYear = int.Parse(form["ExpireYear"]);
            paymentInfo.CreditCardCvv2 = form["CardCode"];
            return paymentInfo;
        }

        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var bluePayPaymentSettings = _settingService.LoadSetting<BluePayPaymentSettings>(storeScope);

            var model = new ConfigurationModel();
            model.UseSandbox = bluePayPaymentSettings.UseSandbox;
            model.TransactModeId = Convert.ToInt32(bluePayPaymentSettings.TransactMode);
            model.AccountId = bluePayPaymentSettings.AccountId;
            model.UserId = bluePayPaymentSettings.UserId;
            model.SecretKey = bluePayPaymentSettings.SecretKey;
            model.AdditionalFee = bluePayPaymentSettings.AdditionalFee;
            model.AdditionalFeePercentage = bluePayPaymentSettings.AdditionalFeePercentage;
            model.TransactModeValues = bluePayPaymentSettings.TransactMode.ToSelectList();

            model.ActiveStoreScopeConfiguration = storeScope;
            if (storeScope > 0)
            {
                model.UseSandbox_OverrideForStore = _settingService.SettingExists(bluePayPaymentSettings, x => x.UseSandbox, storeScope);
                model.TransactModeId_OverrideForStore = _settingService.SettingExists(bluePayPaymentSettings, x => x.TransactMode, storeScope);
                model.AccountId_OverrideForStore = _settingService.SettingExists(bluePayPaymentSettings, x => x.AccountId, storeScope);
                model.UserId_OverrideForStore = _settingService.SettingExists(bluePayPaymentSettings, x => x.UserId, storeScope);
                model.SecretKey_OverrideForStore = _settingService.SettingExists(bluePayPaymentSettings, x => x.SecretKey, storeScope);
                model.AdditionalFee_OverrideForStore = _settingService.SettingExists(bluePayPaymentSettings, x => x.AdditionalFee, storeScope);
                model.AdditionalFeePercentage_OverrideForStore = _settingService.SettingExists(bluePayPaymentSettings, x => x.AdditionalFeePercentage, storeScope);
            }

            return View("~/Plugins/Payments.BluePay/Views/PaymentBluePay/Configure.cshtml", model);
        }

        [HttpPost]
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var bluePayPaymentSettings = _settingService.LoadSetting<BluePayPaymentSettings>(storeScope);

            //save settings
            bluePayPaymentSettings.UseSandbox = model.UseSandbox;
            bluePayPaymentSettings.TransactMode = (TransactMode)model.TransactModeId;
            bluePayPaymentSettings.AccountId = model.AccountId;
            bluePayPaymentSettings.UserId = model.UserId;
            bluePayPaymentSettings.SecretKey = model.SecretKey;
            bluePayPaymentSettings.AdditionalFee = model.AdditionalFee;
            bluePayPaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            if (model.UseSandbox_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(bluePayPaymentSettings, x => x.UseSandbox, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(bluePayPaymentSettings, x => x.UseSandbox, storeScope);

            if (model.TransactModeId_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(bluePayPaymentSettings, x => x.TransactMode, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(bluePayPaymentSettings, x => x.TransactMode, storeScope);

            if (model.AccountId_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(bluePayPaymentSettings, x => x.AccountId, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(bluePayPaymentSettings, x => x.AccountId, storeScope);

            if (model.UserId_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(bluePayPaymentSettings, x => x.UserId, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(bluePayPaymentSettings, x => x.UserId, storeScope);

            if (model.SecretKey_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(bluePayPaymentSettings, x => x.SecretKey, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(bluePayPaymentSettings, x => x.SecretKey, storeScope);

            if (model.AdditionalFee_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(bluePayPaymentSettings, x => x.AdditionalFee, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(bluePayPaymentSettings, x => x.AdditionalFee, storeScope);

            if (model.AdditionalFeePercentage_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(bluePayPaymentSettings, x => x.AdditionalFeePercentage, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(bluePayPaymentSettings, x => x.AdditionalFeePercentage, storeScope);

            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            var model = new PaymentInfoModel();

            //years
            for (int i = 0; i < 15; i++)
            {
                string year = Convert.ToString(DateTime.Now.Year + i);
                model.ExpireYears.Add(new SelectListItem
                {
                    Text = year,
                    Value = year,
                });
            }

            //months
            for (int i = 1; i <= 12; i++)
            {
                string text = (i < 10) ? "0" + i : i.ToString();
                model.ExpireMonths.Add(new SelectListItem
                {
                    Text = text,
                    Value = i.ToString(),
                });
            }

            //set postback values
            var form = this.Request.Form;
            model.CardNumber = form["CardNumber"];
            model.CardCode = form["CardCode"];
            var selectedMonth = model.ExpireMonths.FirstOrDefault(x => x.Value.Equals(form["ExpireMonth"], StringComparison.InvariantCultureIgnoreCase));
            if (selectedMonth != null)
                selectedMonth.Selected = true;
            var selectedYear = model.ExpireYears.FirstOrDefault(x => x.Value.Equals(form["ExpireYear"], StringComparison.InvariantCultureIgnoreCase));
            if (selectedYear != null)
                selectedYear.Selected = true;

            return View("~/Plugins/Payments.BluePay/Views/PaymentBluePay/PaymentInfo.cshtml", model);
        }

        [HttpPost]
        public ActionResult Rebilling(FormCollection parameters)
        {
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var bluePayPaymentSettings = _settingService.LoadSetting<BluePayPaymentSettings>(storeScope);
            var bpManager = new BluePayManager
            {
                AccountId = bluePayPaymentSettings.AccountId,
                UserId = bluePayPaymentSettings.UserId,
                SecretKey = bluePayPaymentSettings.SecretKey
            };

            if (!bpManager.CheckRebillStamp(parameters))
            {
                _logger.Error( "BluePay recurring error: the response has been tampered with");
                return new HttpStatusCodeResult(HttpStatusCode.OK);
            }

            var authId = bpManager.GetAuthorizationIdByRebillId(parameters["rebill_id"]);
            if (string.IsNullOrEmpty(authId))
            {
                _logger.Error(string.Format("BluePay recurring error: the initial transaction for rebill {0} was not found",
                    parameters["rebill_id"]));
                return new HttpStatusCodeResult(HttpStatusCode.OK);
            }

            var initialOrder = _orderService.GetOrderByAuthorizationTransactionIdAndPaymentMethod(authId, "Payments.BluePay");
            if (initialOrder == null)
            {
                _logger.Error(string.Format("BluePay recurring error: the initial order with the AuthorizationTransactionId {0} was not found", 
                    parameters["rebill_id"]));
                return new HttpStatusCodeResult(HttpStatusCode.OK);
            }

            var recurringPayment = _orderService.SearchRecurringPayments(initialOrderId: initialOrder.Id).FirstOrDefault();
            if (recurringPayment != null)
            {
                switch (parameters["status"])
                {
                    case "expired":
                    case "active":
                        _orderProcessingService.ProcessNextRecurringPayment(recurringPayment);
                        var lastPayment = recurringPayment.RecurringPaymentHistory.Last();
                        if (lastPayment != null)
                        {
                            var lastOrder = _orderService.GetOrderById(lastPayment.OrderId);
                            if (lastOrder != null)
                            {
                                lastOrder.PaymentStatus = PaymentStatus.Paid;
                                _orderProcessingService.CheckOrderStatus(lastOrder);
                            }
                        }
                        _logger.Information(string.Format("BluePay recurring order {0} was paid", initialOrder.Id));
                        break;
                    case "failed":
                    case "error":
                        _logger.Error(string.Format("BluePay recurring order {0} {1}", initialOrder.Id, parameters["status"]));
                        break;
                    case "deleted":
                    case "stopped":
                        _orderProcessingService.CancelRecurringPayment(recurringPayment);
                        _logger.Information(string.Format("BluePay recurring order {0} was {1}", initialOrder.Id, parameters["status"]));
                        break;
                    default:
                        break;
                }
            }
            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }
    }
}