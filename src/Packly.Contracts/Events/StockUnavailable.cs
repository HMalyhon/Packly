namespace Packly.Contracts.Events;

/// <summary>
/// At least one line could not be reserved, so nothing was reserved.
/// By this point payment has already been authorised, which is precisely the
/// situation the saga's compensating refund exists to handle.
/// </summary>
/// <param name="OrderId">The order that could not be reserved.</param>
/// <param name="Sku">The first line that could not be satisfied.</param>
/// <param name="Reason">Why the line could not be satisfied.</param>
/// <param name="OccurredAt">When the reservation attempt failed.</param>
public sealed record StockUnavailable(
    Guid OrderId,
    string Sku,
    string Reason,
    DateTimeOffset OccurredAt);
