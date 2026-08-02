using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Packly.Contracts;

namespace Packly.ReadModel;

/// <summary>
/// One order's status as the query side sees it.
/// </summary>
/// <remarks>
/// The read model, and deliberately not a copy of the write model. It holds the
/// answer to a question a customer asks - where is my order, and how did it get
/// there - already assembled, so serving it is a single lookup by id with no
/// joins and nothing to compute.
/// </remarks>
public sealed class OrderStatusDocument
{
    /// <summary>
    /// Gets or sets the order this describes, which is also the document key.
    /// </summary>
    /// <remarks>
    /// The order id is the natural key, so the read model needs no identity of its
    /// own and a lookup by order is a primary key hit.
    /// </remarks>
    [BsonId]
    public Guid OrderId { get; set; }

    /// <summary>Gets or sets the status the order is in now.</summary>
    [BsonRepresentation(BsonType.String)]
    public OrderStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the version of the status change this document reflects.
    /// </summary>
    /// <remarks>
    /// What makes the projection idempotent. An update only applies if it carries
    /// a higher version than the one stored, so a redelivered or late message
    /// cannot move an order backwards or duplicate its history.
    /// </remarks>
    public int Version { get; set; }

    /// <summary>Gets or sets the customer-facing text for the current status.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets when the current status was reached, in UTC.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the statuses recorded for this order, oldest first.
    /// </summary>
    /// <remarks>
    /// Embedded rather than kept in a collection of its own: it is only ever read
    /// alongside the order it belongs to and is bounded by the length of the
    /// workflow. Ordered but not guaranteed complete - a status overtaken in
    /// delivery loses the version comparison and is never recorded.
    /// </remarks>
    public List<OrderStatusHistoryEntry> History { get; set; } = [];
}
