using Packly.Contracts;

namespace Packly.Api.Features.Orders;

/// <summary>
/// One step in an order's journey.
/// </summary>
/// <param name="Status">The status that was reached.</param>
/// <param name="Description">Customer-facing text published with it.</param>
/// <param name="OccurredAt">When it happened.</param>
public sealed record OrderStatusStep(
    OrderStatus Status,
    string Description,
    DateTime OccurredAt);
