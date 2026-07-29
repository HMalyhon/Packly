namespace Packly.Contracts.Events;

/// <summary>The order was picked, packed and handed off for dispatch.</summary>
/// <param name="TrackingNumber">Carrier tracking handle for the parcel.</param>
public sealed record OrderPacked(
    Guid OrderId,
    string TrackingNumber,
    DateTimeOffset PackedAt);
