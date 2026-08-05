using MassTransit;
using MongoDB.Driver;
using Packly.Contracts.Events;
using Packly.ReadModel;

namespace Packly.Projection;

/// <summary>
/// Keeps the read model up to date with the orchestrator's decisions.
/// </summary>
/// <remarks>
/// The seam between the two sides of CQRS, and the reason the read model is
/// eventually consistent: a query issued between a transition and this update
/// still returns the previous status.
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

    // Upsert builds the document it inserts from the equality parts of the filter
    // alone, so a message that lost the version comparison looks like an order with
    // no document yet and collides with the key already there. The duplicate key is
    // the answer, not a failure: a newer version is stored.
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
