using MassTransit;
using Packly.Contracts.Commands;
using Packly.Contracts.Events;

namespace Packly.Inventory;

/// <summary>
/// Picks and packs an order whose stock is already reserved.
/// </summary>
/// <remarks>
/// Packing is a separate command from reservation rather than part of it. They
/// fail for different reasons and at different times, and keeping them apart is
/// what lets the customer be told the order is being packed rather than waiting
/// in silence until it ships.
/// </remarks>
/// <param name="timeProvider">Clock used to stamp results.</param>
/// <param name="logger">Records each parcel.</param>
public sealed class PackOrderConsumer(
    TimeProvider timeProvider,
    ILogger<PackOrderConsumer> logger)
    : IConsumer<PackOrder>
{
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<PackOrder> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var message = context.Message;

        // Deliberately the slowest step. Packing is the one the product is named
        // for, so the order should dwell in it rather than flash past.
        await Task.Delay(Random.Shared.Next(1200, 2500), context.CancellationToken);

        // Version 4, not 7, for the reason spelled out in AuthorizePaymentConsumer:
        // 200 tracking numbers built from a truncated version 7 GUID yielded one
        // distinct value.
        var trackingNumber = $"PK{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        logger.LogInformation(
            "Packed {LineCount} line(s) for order {OrderId} as {TrackingNumber}",
            message.Lines.Count,
            message.OrderId,
            trackingNumber);

        await context.Publish(
            new OrderPacked(message.OrderId, trackingNumber, timeProvider.GetUtcNow()),
            context.CancellationToken);
    }
}
