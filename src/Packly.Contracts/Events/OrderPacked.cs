namespace Packly.Contracts.Events;

/// <summary>The order was picked, packed and handed off for dispatch.</summary>
public sealed record OrderPacked(
    Guid OrderId,
    string TrackingNumber);
