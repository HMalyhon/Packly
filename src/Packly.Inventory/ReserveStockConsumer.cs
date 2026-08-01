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
/// <param name="timeProvider">Clock used to stamp results.</param>
/// <param name="logger">Records each reservation.</param>
public sealed class ReserveStockConsumer(
    TimeProvider timeProvider,
    ILogger<ReserveStockConsumer> logger)
    : IConsumer<ReserveStock>
{
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<ReserveStock> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var message = context.Message;

        // Stands in for talking to a warehouse.
        await Task.Delay(Random.Shared.Next(300, 800), context.CancellationToken);

        logger.LogInformation(
            "Reserved {LineCount} line(s) for order {OrderId}",
            message.Lines.Count,
            message.OrderId);

        await context.Publish(
            new StockReserved(message.OrderId, timeProvider.GetUtcNow()),
            context.CancellationToken);
    }
}
