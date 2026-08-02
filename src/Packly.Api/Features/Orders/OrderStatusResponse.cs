using Packly.Contracts;

namespace Packly.Api.Features.Orders;

/// <summary>
/// Where an order has got to, and how it got there.
/// </summary>
/// <remarks>
/// Served entirely from the read model. Nothing in this response is read from or
/// joined against the write side, which is what the separation is for: queries
/// cannot slow down or lock the tables that accept orders.
/// </remarks>
/// <param name="OrderId">The order this describes.</param>
/// <param name="Status">The status it is in now.</param>
/// <param name="Description">Customer-facing text for the current status.</param>
/// <param name="Version">
/// How many status changes have been applied. A caller polling this endpoint can
/// tell a genuinely unchanged order from a response it has already seen.
/// </param>
/// <param name="UpdatedAt">When the current status was reached.</param>
/// <param name="History">
/// The statuses recorded for this order, oldest first. Ordered rather than
/// complete: the projection discards a status that arrives after a later one, so a
/// step overtaken in delivery is missing here rather than out of place.
/// </param>
public sealed record OrderStatusResponse(
    Guid OrderId,
    OrderStatus Status,
    string Description,
    int Version,
    DateTime UpdatedAt,
    IReadOnlyList<OrderStatusStep> History);
