using Packly.Contracts;

namespace Packly.Api.Features.Orders;

/// <summary>
/// One order as it appears in a list.
/// </summary>
/// <remarks>
/// History is deliberately left out. A list of fifty orders would otherwise carry
/// several hundred steps nobody asked for, and the caller who wants them knows
/// which order to ask about.
/// </remarks>
/// <param name="OrderId">The order.</param>
/// <param name="Status">The status it is in now.</param>
/// <param name="Description">Customer-facing text for the current status.</param>
/// <param name="UpdatedAt">When the current status was reached.</param>
public sealed record OrderSummaryResponse(
    Guid OrderId,
    OrderStatus Status,
    string Description,
    DateTime UpdatedAt);
