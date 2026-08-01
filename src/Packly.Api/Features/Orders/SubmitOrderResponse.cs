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
/// <param name="Status">
/// Always <see cref="OrderStatus.Submitted"/>. Stated rather than omitted because
/// it tells the caller which end of the workflow it is holding; every status after
/// this one comes from the read model, not from here.
/// </param>
/// <param name="Total">The order total, summed from its lines.</param>
public sealed record SubmitOrderResponse(
    Guid OrderId,
    OrderStatus Status,
    decimal Total);
