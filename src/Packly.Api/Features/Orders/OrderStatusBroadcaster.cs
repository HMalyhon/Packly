using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Packly.Contracts.Events;

namespace Packly.Api.Features.Orders;

/// <summary>
/// Carries status changes from the bus to the browsers watching that order.
/// </summary>
/// <remarks>
/// The third subscriber to <see cref="OrderStatusChanged"/>, alongside the
/// notification and projection services. It writes nothing and decides nothing:
/// the orchestrator has already made the decision, the projection has already
/// stored it, and this only shortens the wait between the two.
/// </remarks>
/// <param name="hub">The hub connections are held on.</param>
public sealed class OrderStatusBroadcaster(IHubContext<OrderHub> hub)
    : IConsumer<OrderStatusChanged>
{
    /// <summary>
    /// The name browsers listen for. Sent with the order id as well as the step,
    /// because one connection can be following more than one order.
    /// </summary>
    public const string ClientMethod = "orderStatusChanged";

    /// <inheritdoc />
    public Task Consume(ConsumeContext<OrderStatusChanged> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var message = context.Message;

        return hub.Clients
            .Group(OrderHub.GroupFor(message.OrderId))
            .SendAsync(
                ClientMethod,
                message.OrderId,
                new OrderStatusStep(message.Status, message.Description, message.OccurredAt.UtcDateTime),
                context.CancellationToken);
    }
}
