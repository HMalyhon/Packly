namespace Packly.Contracts.Events;

/// <summary>Every line of the order was reserved from available stock.</summary>
public sealed record StockReserved(
    Guid OrderId,
    DateTimeOffset ReservedAt);
