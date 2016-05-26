using System;
using Nop.Core.Domain.Orders;
using Nop.Core.Events;
using Nop.Services.Events;
using Nop.Services.Orders;

namespace Nop.Plugin.Payments.BluePay.Infrastructure.Cache
{
    /// <summary>
    /// RecurringPaymentInserted event consumer
    /// </summary>
    public partial class RecurringPaymentInsertedConsumer : IConsumer<EntityInserted<RecurringPayment>>
    {
        private readonly IOrderService _orderService;

        public RecurringPaymentInsertedConsumer(IOrderService orderService)
        {
            this._orderService = orderService;
        }

        /// <summary>
        /// Handles the event.
        /// </summary>
        /// <param name="payment">The recurring payment.</param>
        public void HandleEvent(EntityInserted<RecurringPayment> payment)
        {
            var recurringPayment = payment.Entity;
            if (recurringPayment == null)
                return;

            //first payment already was paid on the BluePay, let's add it to history
            if (recurringPayment.RecurringPaymentHistory.Count == 0 &&
                recurringPayment.InitialOrder.PaymentMethodSystemName == "Payments.BluePay")
            {
                recurringPayment.RecurringPaymentHistory.Add(new RecurringPaymentHistory
                    {
                        RecurringPaymentId = recurringPayment.Id,
                        OrderId = recurringPayment.InitialOrderId,
                        CreatedOnUtc = DateTime.UtcNow
                    });
                _orderService.UpdateRecurringPayment(recurringPayment);
            }
        }
    }
}
