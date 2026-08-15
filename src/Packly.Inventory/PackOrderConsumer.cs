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
/// <param name="logger">Records each parcel.</param>
public sealed class PackOrderConsumer(ILogger<PackOrderConsumer> logger)
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

        // Minted per delivery rather than derived from the order, unlike the payment
        // reference: nothing correlates against a tracking number, so a redelivery
        // that mints a second one costs nothing but a different string in a log.
        // Version 4 and not 7, because only the leading characters survive the
        // truncation and those are where a version 7 GUID keeps its millisecond
        // timestamp - 200 of them yielded one distinct value.
        var trackingNumber = $"PK{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        logger.LogInformation(
            "Packed {LineCount} line(s) for order {OrderId} as {TrackingNumber}",
            message.Lines.Count,
            message.OrderId,
            trackingNumber);

        await context.Publish(
            new OrderPacked(message.OrderId, trackingNumber),
            context.CancellationToken);
    }
}
