using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Packly.Contracts;

namespace Packly.ReadModel;

/// <summary>
/// One step in an order's journey, as recorded in the read model.
/// </summary>
public sealed class OrderStatusHistoryEntry
{
    /// <summary>Gets or sets the status that was reached.</summary>
    [BsonRepresentation(BsonType.String)]
    public OrderStatus Status { get; set; }

    /// <summary>Gets or sets the version of the change that recorded it.</summary>
    public int Version { get; set; }

    /// <summary>Gets or sets the customer-facing text published with it.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets when it happened, in UTC.</summary>
    public DateTime OccurredAt { get; set; }
}
