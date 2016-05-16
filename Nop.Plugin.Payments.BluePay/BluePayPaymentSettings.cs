using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.BluePay
{
    public class BluePayPaymentSettings : ISettings
    {
        /// <summary>
        /// Gets or sets a BluePay account ID
        /// </summary>
        public string AccountId { get; set; }

        /// <summary>
        /// Gets or sets a BluePay user ID
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Gets or sets a BluPay secret key
        /// </summary>
        public string SecretKey { get; set; }

        /// <summary>
        /// Gets or sets a transaction mode
        /// </summary>
        public TransactMode TransactMode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use sandbox (testing environment)
        /// </summary>
        public bool UseSandbox { get; set; }

        /// <summary>
        /// Gets or sets an additional fee
        /// </summary>
        public decimal AdditionalFee { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to "additional fee" is specified as percentage. true - percentage, false - fixed value.
        /// </summary>
        public bool AdditionalFeePercentage { get; set; }
    }
}
