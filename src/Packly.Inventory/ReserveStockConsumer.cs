using MassTransit;
using Packly.Contracts.Commands;
using Packly.Contracts.Events;

namespace Packly.Inventory;

/// <summary>
/// Reserves every line of an order from available stock.
/// </summary>
/// <remarks>
/// A stand-in for a warehouse system. Reservation is all-or-nothing: a partly
/// reserved order would leave the workflow holding stock it may never use and no
/// clear answer for the customer.
/// </remarks>
/// <param name="logger">Records each reservation.</param>
public sealed class ReserveStockConsumer(ILogger<ReserveStockConsumer> logger)
    : IConsumer<ReserveStock>
{
    // Encoding the rule in the SKU keeps it in the request: nothing has to be
    // seeded, and the same order always answers the same way - which is what makes
    // a redelivered command a duplicate rather than a coin toss.
    private const string SoldOutPrefix = "SOLD-OUT";

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<ReserveStock> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var message = context.Message;

        // Stands in for talking to a warehouse.
        await Task.Delay(Random.Shared.Next(300, 800), context.CancellationToken);

        var soldOut = message.Lines.FirstOrDefault(
            line => line.Sku.StartsWith(SoldOutPrefix, StringComparison.OrdinalIgnoreCase));

        if (soldOut is not null)
        {
            logger.LogWarning(
                "Cannot reserve {Sku} for order {OrderId}: out of stock",
                soldOut.Sku,
                message.OrderId);

            // Nothing was reserved, so nothing needs releasing here. The order is
            // not this service's to cancel, though: it reports what it found and
            // the saga decides that a refund is owed.
            await context.Publish(
                new StockUnavailable(
                    message.OrderId,
                    soldOut.Sku,
                    "out of stock"),
                context.CancellationToken);

            return;
        }

        logger.LogInformation(
            "Reserved {LineCount} line(s) for order {OrderId}",
            message.Lines.Count,
            message.OrderId);

        await context.Publish(
            new StockReserved(message.OrderId),
            context.CancellationToken);
    }
}
