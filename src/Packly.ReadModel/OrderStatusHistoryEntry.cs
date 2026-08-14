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

    public string Description { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }
}
