using Packly.Contracts;

namespace Packly.Api.Features.Orders;

/// <summary>
/// One order as it appears in a list. History is left out: the caller who wants
/// it knows which order to ask about.
/// </summary>
/// <param name="OrderId">The order.</param>
/// <param name="Status">The status it is in now.</param>
/// <param name="Description">Customer-facing text for the current status.</param>
/// <param name="UpdatedAt">When the current status was reached.</param>
public sealed record OrderSummaryResponse(
    Guid OrderId,
    OrderStatus Status,
    string Description,
    DateTime UpdatedAt);
