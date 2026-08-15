using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Packly.Contracts;

namespace Packly.ReadModel;

/// <summary>
/// One order's status as the query side sees it.
/// </summary>
/// <remarks>
/// Assembled in advance, so serving it is one lookup by id with no joins and
/// nothing to compute.
/// </remarks>
public sealed class OrderStatusDocument
{
    /// <summary>
    /// Gets or sets the order this describes, which is also the document key: the
    /// read model needs no identity of its own.
    /// </summary>
    [BsonId]
    public Guid OrderId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public OrderStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the version of the status change this document reflects. What
    /// the projection compares against to reject a stale one.
    /// </summary>
    public int Version { get; set; }

    public string Description { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets every status recorded for this order.
    /// </summary>
    /// <remarks>
    /// Embedded because it is only ever read alongside its order and is bounded by
    /// the length of the workflow. Complete but unordered: a step is recorded
    /// whether or not it wins the comparison that moves
    /// <see cref="Status"/> forward, and it is stored in the order it arrived.
    /// Sort by <see cref="OrderStatusHistoryEntry.Version"/> to read it back as the
    /// workflow ran.
    /// </remarks>
    public List<OrderStatusHistoryEntry> History { get; set; } = [];
}
