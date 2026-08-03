using MassTransit;
using MongoDB.Driver;
using Packly.Contracts.Events;
using Packly.ReadModel;

namespace Packly.Projection;

/// <summary>
/// Keeps the read model up to date with the orchestrator's decisions.
/// </summary>
/// <remarks>
/// <para>
/// The seam between the two sides of CQRS. The write side is normalised, owns the
/// order aggregate and answers no questions; this side is denormalised, owns
/// nothing and answers all of them. Neither can corrupt the other, because the
/// only thing crossing between them is an event.
/// </para>
/// <para>
/// The read model is therefore eventually consistent, and a query issued in the
/// instant between a transition and this update returns the previous status. That
/// is the price of the split, and the reason the version below matters more than
/// it looks.
/// </para>
/// </remarks>
/// <param name="collection">The read model collection.</param>
/// <param name="logger">Records what was applied and what was discarded.</param>
public sealed class OrderStatusChangedConsumer(
    IMongoCollection<OrderStatusDocument> collection,
    ILogger<OrderStatusChangedConsumer> logger)
    : IConsumer<OrderStatusChanged>
{
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<OrderStatusChanged> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var message = context.Message;

        // Matches only an older version of this order, so a redelivered or
        // overtaken message finds nothing to update. Delivery is at-least-once and
        // unordered, so without this an order could be shown moving from Completed
        // back to Packing, and its history could gain the same step twice.
        var filter = Builders<OrderStatusDocument>.Filter.And(
            Builders<OrderStatusDocument>.Filter.Eq(document => document.OrderId, message.OrderId),
            Builders<OrderStatusDocument>.Filter.Lt(document => document.Version, message.Version));

        var update = Builders<OrderStatusDocument>.Update
            .Set(document => document.Status, message.Status)
            .Set(document => document.Version, message.Version)
            .Set(document => document.Description, message.Description)
            .Set(document => document.UpdatedAt, message.OccurredAt.UtcDateTime)
            .Push(
                document => document.History,
                new OrderStatusHistoryEntry
                {
                    Status = message.Status,
                    Version = message.Version,
                    Description = message.Description,
                    OccurredAt = message.OccurredAt.UtcDateTime,
                });

        if (!await TryApplyAsync(filter, update, context.CancellationToken))
        {
            logger.LogInformation(
                "Discarded status version {Version} for order {OrderId}: already superseded",
                message.Version,
                message.OrderId);

            return;
        }

        logger.LogInformation(
            "Projected order {OrderId} as {Status} at version {Version}",
            message.OrderId,
            message.Status,
            message.Version);
    }

    /// <summary>
    /// Applies the update, reporting whether it was still the newest word on this
    /// order.
    /// </summary>
    /// <remarks>
    /// Upsert builds the document it inserts from the equality parts of the filter
    /// alone, so a message that lost the version comparison is treated as an order
    /// that has no document yet and the insert collides with the key already
    /// there. The duplicate key is the answer rather than a failure: a newer
    /// version is stored, and this one has nothing to add.
    /// </remarks>
    /// <param name="filter">Matches the order only at an older version.</param>
    /// <param name="update">The change to apply.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns><see langword="true"/> if the update was applied.</returns>
    private async Task<bool> TryApplyAsync(
        FilterDefinition<OrderStatusDocument> filter,
        UpdateDefinition<OrderStatusDocument> update,
        CancellationToken cancellationToken)
    {
        try
        {
            await collection.UpdateOneAsync(
                filter,
                update,
                new UpdateOptions { IsUpsert = true },
                cancellationToken);

            return true;
        }
        catch (MongoWriteException exception)
            when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }
}
