namespace Packly.Contracts.Events;

/// <summary>The order was picked, packed and handed off for dispatch.</summary>
/// <param name="OrderId">The order that was packed.</param>
/// <param name="TrackingNumber">Carrier tracking handle for the parcel.</param>
/// <param name="PackedAt">When packing finished.</param>
public sealed record OrderPacked(
    Guid OrderId,
    string TrackingNumber,
    DateTimeOffset PackedAt);
