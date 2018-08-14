using System;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.BluePay.Models;
using Nop.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.BluePay.Controllers
{
    public class PaymentBluePayController : BasePaymentController
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly ISettingService _settingService;
        private readonly IPermissionService _permissionService;
        private readonly IStoreContext _storeContext;

        #endregion

        #region Ctor

        public PaymentBluePayController(ILocalizationService localizationService,
            ILogger logger,
            IOrderProcessingService orderProcessingService,
            IOrderService orderService,
            ISettingService settingService,
            IPermissionService permissionService,
            IStoreContext storeContext)
        {
            this._localizationService = localizationService;
            this._logger = logger;
            this._orderProcessingService = orderProcessingService;
            this._orderService = orderService;
            this._settingService = settingService;
            this._permissionService = permissionService;
            this._storeContext = storeContext;
        }

        #endregion

        #region Methods

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var bluePayPaymentSettings = _settingService.LoadSetting<BluePayPaymentSettings>(storeScope);

            var model = new ConfigurationModel
            {
                UseSandbox = bluePayPaymentSettings.UseSandbox,
                TransactModeId = Convert.ToInt32(bluePayPaymentSettings.TransactMode),
                AccountId = bluePayPaymentSettings.AccountId,
                UserId = bluePayPaymentSettings.UserId,
                SecretKey = bluePayPaymentSettings.SecretKey,
                AdditionalFee = bluePayPaymentSettings.AdditionalFee,
                AdditionalFeePercentage = bluePayPaymentSettings.AdditionalFeePercentage,
                TransactModeValues = bluePayPaymentSettings.TransactMode.ToSelectList(),
                ActiveStoreScopeConfiguration = storeScope
            };
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

            return View("~/Plugins/Payments.BluePay/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure(ConfigurationModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
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
            _settingService.SaveSettingOverridablePerStore(bluePayPaymentSettings, x => x.UseSandbox, model.UseSandbox_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(bluePayPaymentSettings, x => x.TransactMode, model.TransactModeId_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(bluePayPaymentSettings, x => x.AccountId, model.AccountId_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(bluePayPaymentSettings, x => x.UserId, model.UserId_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(bluePayPaymentSettings, x => x.SecretKey, model.SecretKey_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(bluePayPaymentSettings, x => x.AdditionalFee, model.AdditionalFee_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(bluePayPaymentSettings, x => x.AdditionalFeePercentage, model.AdditionalFeePercentage_OverrideForStore, storeScope, false);

            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }
        
        [HttpPost]
        public ActionResult Rebilling(IpnModel model)
        {
            var parameters = model.Form;

            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var bluePayPaymentSettings = _settingService.LoadSetting<BluePayPaymentSettings>(storeScope);
            var bpManager = new BluePayManager
            {
                AccountId = bluePayPaymentSettings.AccountId,
                UserId = bluePayPaymentSettings.UserId,
                SecretKey = bluePayPaymentSettings.SecretKey
            };

            if (!bpManager.CheckRebillStamp(parameters))
            {
                _logger.Error("BluePay recurring error: the response has been tampered with");
                return new StatusCodeResult((int)HttpStatusCode.OK);
            }

            var authId = bpManager.GetAuthorizationIdByRebillId(parameters["rebill_id"]);
            if (string.IsNullOrEmpty(authId))
            {
                _logger.Error($"BluePay recurring error: the initial transaction for rebill {parameters["rebill_id"]} was not found");
                return new StatusCodeResult((int)HttpStatusCode.OK);
            }

            var initialOrder = _orderService.GetOrderByAuthorizationTransactionIdAndPaymentMethod(authId, "Payments.BluePay");
            if (initialOrder == null)
            {
                _logger.Error($"BluePay recurring error: the initial order with the AuthorizationTransactionId {parameters["rebill_id"]} was not found");
                return new StatusCodeResult((int)HttpStatusCode.OK);
            }

            var recurringPayment = _orderService.SearchRecurringPayments(initialOrderId: initialOrder.Id).FirstOrDefault();
            var processPaymentResult = new ProcessPaymentResult();
            if (recurringPayment != null)
            {
                switch (parameters["status"])
                {
                    case "expired":
                    case "active":
                        processPaymentResult.NewPaymentStatus = PaymentStatus.Paid;
                        _orderProcessingService.ProcessNextRecurringPayment(recurringPayment, processPaymentResult);
                        break;
                    case "failed":
                    case "error":
                        processPaymentResult.RecurringPaymentFailed = true;
                        processPaymentResult.Errors.Add($"BluePay recurring order {initialOrder.Id} {parameters["status"]}");
                        _orderProcessingService.ProcessNextRecurringPayment(recurringPayment, processPaymentResult);
                        break;
                    case "deleted":
                    case "stopped":
                        _orderProcessingService.CancelRecurringPayment(recurringPayment);
                        _logger.Information($"BluePay recurring order {initialOrder.Id} was {parameters["status"]}");
                        break;
                }
            }

            return new StatusCodeResult((int)HttpStatusCode.OK);
        }

        #endregion
    }
}