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
    // One to try, one to settle a race it may have lost. See RecordHistoryAsync.
    private const int InsertRaceAttempts = 2;

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<OrderStatusChanged> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var message = context.Message;

        // Two writes, because the two facts have different rules. A step always
        // happened, so it is always recorded; the current status is only this
        // message's if no newer one has arrived. Deciding both with one version
        // comparison meant an overtaken message lost its place in the history as
        // well as the argument about what the status is - so an order that caught
        // up after downtime displayed two of its five steps.
        await RecordHistoryAsync(message, context.CancellationToken);

        if (await TryAdvanceStatusAsync(message, context.CancellationToken))
        {
            logger.LogInformation(
                "Projected order {OrderId} as {Status} at version {Version}",
                message.OrderId,
                message.Status,
                message.Version);

            return;
        }

        logger.LogInformation(
            "Recorded version {Version} for order {OrderId} without advancing it: already superseded",
            message.Version,
            message.OrderId);
    }

    /// <summary>
    /// Records the step, whether or not it is the newest one.
    /// </summary>
    /// <remarks>
    /// AddToSet rather than Push, so a redelivery of the same message does not
    /// record the same step twice - the entry is identical, and a set ignores it.
    /// <para>
    /// Upsert creates the document from the id in the filter, which is what lets a
    /// message arriving before the order's first status still be recorded. Two
    /// messages for an order with no document yet can both attempt that insert, and
    /// the loser collides on the key; it simply goes round again, and the second
    /// attempt takes the update path where no insert - and so no collision - is
    /// possible.
    /// </para>
    /// </remarks>
    private async Task RecordHistoryAsync(
        OrderStatusChanged message,
        CancellationToken cancellationToken)
    {
        var filter = Builders<OrderStatusDocument>.Filter.Eq(
            document => document.OrderId, message.OrderId);

        // The scalar fields are seeded only on insert, and only so a fresh document
        // is never briefly readable with no status at all. Whatever they are set to
        // here, TryAdvanceStatusAsync settles them immediately afterwards.
        var update = Builders<OrderStatusDocument>.Update
            .AddToSet(
                document => document.History,
                new OrderStatusHistoryEntry
                {
                    Status = message.Status,
                    Version = message.Version,
                    Description = message.Description,
                    OccurredAt = message.OccurredAt.UtcDateTime,
                })
            .SetOnInsert(document => document.Version, 0)
            .SetOnInsert(document => document.Status, message.Status)
            .SetOnInsert(document => document.Description, message.Description)
            .SetOnInsert(document => document.UpdatedAt, message.OccurredAt.UtcDateTime);

        for (var attempt = 1; attempt <= InsertRaceAttempts; attempt++)
        {
            try
            {
                await collection.UpdateOneAsync(
                    filter,
                    update,
                    new UpdateOptions { IsUpsert = true },
                    cancellationToken);

                return;
            }
            catch (MongoWriteException exception)

                // Null when the write failed only on write concern, which is not a
                // duplicate key. Checked with ?. because an exception thrown inside
                // a filter is swallowed and reads as a non-match, which would let an
                // unrelated fault through here looking like a handled one.
                when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey
                    && attempt < InsertRaceAttempts)
            {
                // Someone else inserted first. Round again against their document.
            }
        }
    }

    /// <summary>
    /// Moves the current status forward, if this message is newer than what is
    /// stored.
    /// </summary>
    /// <remarks>
    /// No upsert: <see cref="RecordHistoryAsync"/> has already guaranteed the
    /// document exists, so this cannot insert and cannot collide. Delivery is
    /// at-least-once and unordered, so without the version comparison an order could
    /// be shown moving from Completed back to Packing.
    /// </remarks>
    /// <returns><c>true</c> if the status moved; otherwise <c>false</c>.</returns>
    private async Task<bool> TryAdvanceStatusAsync(
        OrderStatusChanged message,
        CancellationToken cancellationToken)
    {
        var filter = Builders<OrderStatusDocument>.Filter.And(
            Builders<OrderStatusDocument>.Filter.Eq(
                document => document.OrderId, message.OrderId),
            Builders<OrderStatusDocument>.Filter.Lt(
                document => document.Version, message.Version));

        var update = Builders<OrderStatusDocument>.Update
            .Set(document => document.Status, message.Status)
            .Set(document => document.Version, message.Version)
            .Set(document => document.Description, message.Description)
            .Set(document => document.UpdatedAt, message.OccurredAt.UtcDateTime);

        var result = await collection.UpdateOneAsync(
            filter, update, cancellationToken: cancellationToken);

        return result.ModifiedCount > 0;
    }
}
