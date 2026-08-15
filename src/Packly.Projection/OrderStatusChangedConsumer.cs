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
    // One to try, one to settle the race it may have lost. See TryApplyAsync.
    private const int InsertRaceAttempts = 2;

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

    // Two different situations raise the same duplicate key, and only one of them
    // means the update is superseded.
    //
    // Upsert builds the document it inserts from the equality parts of the filter
    // alone, so any message the filter does not match looks like an order with no
    // document yet and tries to insert. When a document is already stored at a
    // newer version, that collision is the answer: nothing older should be applied.
    //
    // When no document exists yet, though, two messages for the same order can both
    // miss the filter and both try to insert. One wins and the other collides - and
    // the one that collides may be the newer of the two. Discarding it there would
    // drop a status permanently, silently, and the read model has no replay to
    // recover it. So a collision is retried rather than believed: the second attempt
    // sees the document the winner wrote, and the version comparison makes the real
    // decision. Two attempts is the whole of it, because after the first there is
    // always a document to compare against.
    private async Task<bool> TryApplyAsync(
        FilterDefinition<OrderStatusDocument> filter,
        UpdateDefinition<OrderStatusDocument> update,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= InsertRaceAttempts; attempt++)
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

                // Null when the write failed only on write concern, which is not a
                // duplicate key. Checked with ?. because an exception thrown inside
                // a filter is swallowed and reads as a non-match, which would let an
                // unrelated fault through here looking like a handled one.
                when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                // Round again: a document exists now, so the filter decides.
            }
        }

        return false;
    }
}
