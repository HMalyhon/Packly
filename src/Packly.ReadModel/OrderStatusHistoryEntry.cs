using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Packly.Contracts;

namespace Packly.ReadModel;

/// <summary>
/// One step in an order's journey, as recorded in the read model.
/// </summary>
public sealed class OrderStatusHistoryEntry
{
    [BsonRepresentation(BsonType.String)]
    public OrderStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the status version this step records.
    /// </summary>
    /// <remarks>
    /// Steps are recorded as they arrive, and delivery is unordered, so the stored
    /// array is not in workflow order. This is what puts it back in one - a total
    /// order that two steps published in the same millisecond cannot tie on, which
    /// is why it is here rather than sorting on <see cref="OccurredAt"/>.
    /// </remarks>
    public int Version { get; set; }

    public string Description { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }
}
