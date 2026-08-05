using Packly.Contracts;

namespace Packly.Api.Features.Orders;

/// <summary>
/// Where an order has got to, and how it got there.
/// </summary>
/// <param name="OrderId">The order this describes.</param>
/// <param name="Status">The status it is in now.</param>
/// <param name="Description">Customer-facing text for the current status.</param>
/// <param name="Version">
/// How many status changes have been applied. Lets a caller polling this endpoint
/// tell an unchanged order from a response it has already seen.
/// </param>
/// <param name="UpdatedAt">When the current status was reached.</param>
/// <param name="History">
/// The statuses recorded for this order, oldest first. Ordered rather than
/// complete: a step overtaken in delivery is missing rather than out of place.
/// </param>
public sealed record OrderStatusResponse(
    Guid OrderId,
    OrderStatus Status,
    string Description,
    int Version,
    DateTime UpdatedAt,
    IReadOnlyList<OrderStatusStep> History);
