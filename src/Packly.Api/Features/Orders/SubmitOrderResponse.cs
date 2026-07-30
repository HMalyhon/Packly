using Packly.Contracts;

namespace Packly.Api.Features.Orders;

/// <summary>
/// What the caller gets back after placing an order.
/// </summary>
/// <remarks>
/// The order is accepted, not completed: payment and stock happen afterwards, on
/// other services. The caller receives an identifier to follow the order with and
/// should expect the status to change.
/// </remarks>
/// <param name="OrderId">Identifier to track the order by.</param>
/// <param name="Status">The status at the moment of acceptance.</param>
/// <param name="Total">The order total, summed from its lines.</param>
public sealed record SubmitOrderResponse(
    Guid OrderId,
    OrderStatus Status,
    decimal Total);
