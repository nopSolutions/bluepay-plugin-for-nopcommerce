using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Plugins;
using Nop.Plugin.Payments.BluePay.Controllers;
using Nop.Plugin.Payments.BluePay.Models;
using Nop.Plugin.Payments.BluePay.Validators;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Payments;

namespace Nop.Plugin.Payments.BluePay
{
    /// <summary>
    /// BluePay payment processor
    /// </summary>
    public class BluePayPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly BluePayPaymentSettings _bluePayPaymentSettings;
        private readonly ICurrencyService _currencyService;
        private readonly ICustomerService _customerService;
        private readonly ISettingService _settingService;
        private readonly IPaymentService _paymentService;
        private readonly IWebHelper _webHelper;
        private readonly ILocalizationService _localizationService;

        #endregion

        #region Ctor

        public BluePayPaymentProcessor(BluePayPaymentSettings bluePayPaymentSettings,
            ICurrencyService currencyService,
            ICustomerService customerService,
            ISettingService settingService,
            IPaymentService paymentService,
            IWebHelper webHelper,
            ILocalizationService localizationService)
        {
            this._bluePayPaymentSettings = bluePayPaymentSettings;
            this._currencyService = currencyService;
            this._customerService = customerService;
            this._settingService = settingService;
            this._paymentService = paymentService;
            this._webHelper = webHelper;
            this._localizationService = localizationService;
        }

        #endregion

        #region Properties

        public string GetPublicViewComponentName()
        {
            return "PaymentBluePay";
        }

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get { return true; }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get { return true; }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get { return true; }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get { return true; }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get { return RecurringPaymentType.Automatic; }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get { return PaymentMethodType.Standard; }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public string PaymentMethodDescription
        {
            get { return _localizationService.GetResource("Plugins.Payments.BluePay.PaymentMethodDescription"); }
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Get amount in the USD currency
        /// </summary>
        /// <param name="amount">Amount</param>
        /// <returns>Amount in the USD currency</returns>
        private decimal GetUsdAmount(decimal amount)
        {
            var usd = _currencyService.GetCurrencyByCode("USD");
            if (usd == null)
                throw new Exception("USD currency could not be loaded");

            return _currencyService.ConvertFromPrimaryStoreCurrency(amount, usd);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var customer = _customerService.GetCustomerById(processPaymentRequest.CustomerId);
            if (customer == null)
                throw new Exception("Customer cannot be loaded");

            var result = new ProcessPaymentResult();
            var bpManager = new BluePayManager
            {
                AccountId = _bluePayPaymentSettings.AccountId,
                UserId = _bluePayPaymentSettings.UserId,
                SecretKey = _bluePayPaymentSettings.SecretKey,
                IsSandbox = _bluePayPaymentSettings.UseSandbox,
                CustomerIP = _webHelper.GetCurrentIpAddress(),
                CustomId1 = customer.Id.ToString(),
                CustomId2 = customer.CustomerGuid.ToString(),
                FirstName = customer.BillingAddress.FirstName,
                LastName = customer.BillingAddress.LastName,
                Email = customer.BillingAddress.Email,
                Address1 = customer.BillingAddress.Address1,
                Address2 = customer.BillingAddress.Address2,
                City = customer.BillingAddress.City,
                Country = customer.BillingAddress.Country?.ThreeLetterIsoCode,
                Zip = customer.BillingAddress.ZipPostalCode,
                Phone = customer.BillingAddress.PhoneNumber,
                State = customer.BillingAddress.StateProvince?.Abbreviation,
                CardNumber = processPaymentRequest.CreditCardNumber,
                CardExpire = $"{new DateTime(processPaymentRequest.CreditCardExpireYear, processPaymentRequest.CreditCardExpireMonth, 1):MMyy}",
                CardCvv2 = processPaymentRequest.CreditCardCvv2,
                Amount = GetUsdAmount(processPaymentRequest.OrderTotal).ToString("F", new CultureInfo("en-US")),
                OrderId = processPaymentRequest.OrderGuid.ToString(),
                InvoiceId = processPaymentRequest.OrderGuid.ToString()
            };

            bpManager.Sale(_bluePayPaymentSettings.TransactMode == TransactMode.AuthorizeAndCapture);

            if (bpManager.IsSuccessful)
            {
                result.AvsResult = bpManager.AVS;
                result.AuthorizationTransactionCode = bpManager.AuthCode;
                if (_bluePayPaymentSettings.TransactMode == TransactMode.AuthorizeAndCapture)
                {
                    result.CaptureTransactionId = bpManager.TransactionId;
                    result.CaptureTransactionResult = bpManager.Message;
                    result.NewPaymentStatus = PaymentStatus.Paid;
                }
                else
                {
                    result.AuthorizationTransactionId = bpManager.TransactionId;
                    result.AuthorizationTransactionResult = bpManager.Message;
                    result.NewPaymentStatus = PaymentStatus.Authorized;
                }
            }
            else
                result.AddError(bpManager.Message);

            return result;
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //nothing
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            var bpManager = new BluePayManager
            {
                AccountId = _bluePayPaymentSettings.AccountId,
                UserId = _bluePayPaymentSettings.UserId,
                SecretKey = _bluePayPaymentSettings.SecretKey,
                IsSandbox = _bluePayPaymentSettings.UseSandbox,
                MasterId = capturePaymentRequest.Order.AuthorizationTransactionId,
                Amount = GetUsdAmount(capturePaymentRequest.Order.OrderTotal).ToString("F", new CultureInfo("en-US"))
            };

            bpManager.Capture();

            if (bpManager.IsSuccessful)
            {
                result.NewPaymentStatus = PaymentStatus.Paid;
                result.CaptureTransactionId = bpManager.TransactionId;
                result.CaptureTransactionResult = bpManager.Message;
            }
            else
                result.AddError(bpManager.Message);

            return result;
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            var bpManager = new BluePayManager
            {
                AccountId = _bluePayPaymentSettings.AccountId,
                UserId = _bluePayPaymentSettings.UserId,
                SecretKey = _bluePayPaymentSettings.SecretKey,
                IsSandbox = _bluePayPaymentSettings.UseSandbox,
                MasterId = refundPaymentRequest.Order.CaptureTransactionId,
                Amount = refundPaymentRequest.IsPartialRefund ? GetUsdAmount(refundPaymentRequest.AmountToRefund).ToString("F", new CultureInfo("en-US")) : null
            };

            bpManager.Refund();

            if (!bpManager.IsSuccessful)
                result.AddError(bpManager.Message);
            else
                result.NewPaymentStatus = refundPaymentRequest.IsPartialRefund 
                    && refundPaymentRequest.Order.RefundedAmount + refundPaymentRequest.AmountToRefund < refundPaymentRequest.Order.OrderTotal 
                    ? PaymentStatus.PartiallyRefunded : PaymentStatus.Refunded;
            
            return result;
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            var bpManager = new BluePayManager
            {
                AccountId = _bluePayPaymentSettings.AccountId,
                UserId = _bluePayPaymentSettings.UserId,
                SecretKey = _bluePayPaymentSettings.SecretKey,
                IsSandbox = _bluePayPaymentSettings.UseSandbox,
                MasterId = !string.IsNullOrEmpty(voidPaymentRequest.Order.AuthorizationTransactionId) ?
                    voidPaymentRequest.Order.AuthorizationTransactionId : voidPaymentRequest.Order.CaptureTransactionId
            };

            bpManager.Void();

            if (bpManager.IsSuccessful)
                result.NewPaymentStatus = PaymentStatus.Voided;
            else
                result.AddError(bpManager.Message);

            return result;
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var customer = _customerService.GetCustomerById(processPaymentRequest.CustomerId);
            if (customer == null)
                throw new Exception("Customer cannot be loaded");

            var result = new ProcessPaymentResult();
            var bpManager = new BluePayManager
            {
                AccountId = _bluePayPaymentSettings.AccountId,
                UserId = _bluePayPaymentSettings.UserId,
                SecretKey = _bluePayPaymentSettings.SecretKey,
                IsSandbox = _bluePayPaymentSettings.UseSandbox,
                CustomerIP = _webHelper.GetCurrentIpAddress(),
                CustomId1 = customer.Id.ToString(),
                CustomId2 = customer.CustomerGuid.ToString(),
                FirstName = customer.BillingAddress.FirstName,
                LastName = customer.BillingAddress.LastName,
                Email = customer.BillingAddress.Email,
                Address1 = customer.BillingAddress.Address1,
                Address2 = customer.BillingAddress.Address2,
                City = customer.BillingAddress.City,
                Country = customer.BillingAddress.Country != null ? customer.BillingAddress.Country.ThreeLetterIsoCode : null,
                Zip = customer.BillingAddress.ZipPostalCode,
                Phone = customer.BillingAddress.PhoneNumber,
                State = customer.BillingAddress.StateProvince != null ? customer.BillingAddress.StateProvince.Abbreviation : null,
                CardNumber = processPaymentRequest.CreditCardNumber,
                CardExpire = $"{new DateTime(processPaymentRequest.CreditCardExpireYear, processPaymentRequest.CreditCardExpireMonth, 1):MMyy}",
                CardCvv2 = processPaymentRequest.CreditCardCvv2,
                Amount = GetUsdAmount(processPaymentRequest.OrderTotal).ToString("F", new CultureInfo("en-US")),
                OrderId = processPaymentRequest.OrderGuid.ToString(),
                InvoiceId = processPaymentRequest.OrderGuid.ToString(),
                DoRebill = "1",
                RebillAmount = GetUsdAmount(processPaymentRequest.OrderTotal).ToString("F", new CultureInfo("en-US")),
                RebillCycles = processPaymentRequest.RecurringTotalCycles > 0 ? (processPaymentRequest.RecurringTotalCycles - 1).ToString() : null,
                RebillFirstDate = $"{processPaymentRequest.RecurringCycleLength} {processPaymentRequest.RecurringCyclePeriod.ToString().TrimEnd('s').ToUpperInvariant()}",
                RebillExpression = $"{processPaymentRequest.RecurringCycleLength} {processPaymentRequest.RecurringCyclePeriod.ToString().TrimEnd('s').ToUpperInvariant()}"
            };

            bpManager.SaleRecurring();

            if (bpManager.IsSuccessful)
            {
                result.NewPaymentStatus = PaymentStatus.Paid;
                result.SubscriptionTransactionId = bpManager.RebillId;
                result.AuthorizationTransactionCode = bpManager.AuthCode;
                result.AvsResult = bpManager.AVS;
                result.AuthorizationTransactionId = bpManager.TransactionId;
                result.CaptureTransactionId = bpManager.TransactionId;
                result.CaptureTransactionResult = bpManager.Message;
            }
            else
                result.AddError(bpManager.Message);

            return result;
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();
            var bpManager = new BluePayManager
            {
                AccountId = _bluePayPaymentSettings.AccountId,
                UserId = _bluePayPaymentSettings.UserId,
                SecretKey = _bluePayPaymentSettings.SecretKey,
                MasterId = cancelPaymentRequest.Order.SubscriptionTransactionId
            };

            bpManager.CancelRecurring();

            if (!bpManager.IsSuccessfulCancelRecurring)
                result.AddError(bpManager.Message);

            return result;
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            return false;
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return false;
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            var result = _paymentService.CalculateAdditionalFee(cart,
                _bluePayPaymentSettings.AdditionalFee, _bluePayPaymentSettings.AdditionalFeePercentage);
            return result;
        }

        /// <summary>
        /// Validate payment form
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>List of validating errors</returns>
        public IList<string> ValidatePaymentForm(IFormCollection form)
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
                warnings.AddRange(validationResult.Errors.Select(error => error.ErrorMessage));

            return warnings;
        }

        /// <summary>
        /// Get payment information
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>Payment info holder</returns>
        public ProcessPaymentRequest GetPaymentInfo(IFormCollection form)
        {
            return new ProcessPaymentRequest
            {
                CreditCardNumber = form["CardNumber"],
                CreditCardExpireMonth = int.Parse(form["ExpireMonth"]),
                CreditCardExpireYear = int.Parse(form["ExpireYear"]),
                CreditCardCvv2 = form["CardCode"]
            };
        }

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentBluePay/Configure";
        }

        /// <summary>
        /// Get type of controller
        /// </summary>
        /// <returns>Controller type</returns>
        public Type GetControllerType()
        {
            return typeof(PaymentBluePayController);
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            //settings
            _settingService.SaveSetting(new BluePayPaymentSettings
            {
                TransactMode = TransactMode.Authorize,
                UseSandbox = true
            });

            //locales
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.BluePay.Fields.AccountId", "Account ID");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.BluePay.Fields.AccountId.Hint", "Specify BluePay account number.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.BluePay.Fields.AdditionalFee", "Additional fee");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.BluePay.Fields.AdditionalFee.Hint", "Enter additional fee to charge your customers.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.BluePay.Fields.AdditionalFeePercentage", "Additional fee. Use percentage");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.BluePay.Fields.AdditionalFeePercentage.Hint", "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.BluePay.Fields.SecretKey", "Secret key");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.BluePay.Fields.SecretKey.Hint", "Specify API secret key.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.BluePay.Fields.TransactMode", "Transaction mode");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.BluePay.Fields.TransactMode.Hint", "Specify transaction mode.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.BluePay.Fields.UserId", "User ID");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.BluePay.Fields.UserId.Hint", "Specify BluePay user number.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.BluePay.Fields.UseSandbox", "Use sandbox");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.BluePay.Fields.UseSandbox.Hint", "Check to enable sandbox (testing environment).");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.BluePay.PaymentMethodDescription", "Pay by credit / debit card");

            base.Install();
        }
        
        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<BluePayPaymentSettings>();

            //locales
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.BluePay.Fields.AccountId");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.BluePay.Fields.AccountId.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.BluePay.Fields.AdditionalFee");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.BluePay.Fields.AdditionalFee.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.BluePay.Fields.AdditionalFeePercentage");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.BluePay.Fields.AdditionalFeePercentage.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.BluePay.Fields.SecretKey");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.BluePay.Fields.SecretKey.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.BluePay.Fields.TransactMode");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.BluePay.Fields.TransactMode.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.BluePay.Fields.UserId");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.BluePay.Fields.UserId.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.BluePay.Fields.UseSandbox");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.BluePay.Fields.UseSandbox.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.BluePay.PaymentMethodDescription");

            base.Uninstall();
        }

        #endregion
    }
}