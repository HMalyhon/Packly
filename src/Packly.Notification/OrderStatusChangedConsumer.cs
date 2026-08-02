using MassTransit;
using Packly.Contracts;
using Packly.Contracts.Events;

namespace Packly.Notification;

/// <summary>
/// Tells the customer when their order has reached an outcome.
/// </summary>
/// <remarks>
/// <para>
/// A stand-in for whatever actually sends mail. What matters here is the shape:
/// this service subscribes to a published event and the orchestrator has no idea
/// it exists. It can be stopped, restarted or removed and the workflow is
/// unaffected, which is the practical difference between publishing an event and
/// sending a command.
/// </para>
/// <para>
/// Every transition arrives here, not only the interesting ones. Messages are
/// routed by type and the status is a field rather than a type of its own, so the
/// choice of what deserves an email is made in code. Splitting the event into one
/// type per status would let the broker do the filtering, at the cost of a
/// contract that changes shape every time the workflow gains a step.
/// </para>
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

            // Everything else is a step on the way, not an outcome. Mailing on
            // each one would be four emails per order and would make the two that
            // matter easy to miss.
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
