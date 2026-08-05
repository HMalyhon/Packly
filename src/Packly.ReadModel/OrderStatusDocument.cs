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

    /// <summary>Gets or sets the status the order is in now.</summary>
    [BsonRepresentation(BsonType.String)]
    public OrderStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the version of the status change this document reflects. What
    /// the projection compares against to reject a stale one.
    /// </summary>
    public int Version { get; set; }

    /// <summary>Gets or sets the customer-facing text for the current status.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets when the current status was reached, in UTC.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the statuses recorded for this order, oldest first.
    /// </summary>
    /// <remarks>
    /// Embedded because it is only ever read alongside its order and is bounded by
    /// the length of the workflow. Ordered but not guaranteed complete: a status
    /// overtaken in delivery loses the version comparison and is never recorded.
    /// </remarks>
    public List<OrderStatusHistoryEntry> History { get; set; } = [];
}
