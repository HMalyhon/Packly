using MassTransit;
using Packly.Contracts;
using Packly.Contracts.Events;

namespace Packly.Notification;

/// <summary>
/// Tells the customer when their order has reached an outcome.
/// </summary>
/// <remarks>
/// A stand-in for whatever actually sends mail. Every transition arrives here,
/// not only the interesting ones: messages route by type and the status is a
/// field, so the choice of what deserves an email is made below rather than by
/// the broker.
/// </remarks>
/// <param name="logger">Stands in for the mail transport.</param>
public sealed class OrderStatusChangedConsumer(ILogger<OrderStatusChangedConsumer> logger)
    : IConsumer<OrderStatusChanged>
{
    /// <inheritdoc />
    public Task Consume(ConsumeContext<OrderStatusChanged> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var message = context.Message;

        var subject = message.Status switch
        {
            OrderStatus.Completed => "Your order is on its way",
            OrderStatus.Rejected => "We could not take payment for your order",
            OrderStatus.Cancelled => "Your order was cancelled and refunded",

            // Everything else is a step on the way, not an outcome. Exactly one of
            // the three above happens per order; mailing the rest as well would
            // bury it under progress reports nobody asked for.
            _ => null,
        };

        if (subject is null)
        {
            return Task.CompletedTask;
        }

        logger.LogInformation(
            "Email to customer of order {OrderId}: \"{Subject}\" - {Body}",
            message.OrderId,
            subject,
            message.Description);

        return Task.CompletedTask;
    }
}
