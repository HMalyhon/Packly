using Packly.Contracts;

namespace Packly.Api.Features.Orders;

/// <summary>
/// What the caller gets back after placing an order: accepted, not completed.
/// </summary>
/// <param name="OrderId">Identifier to track the order by.</param>
/// <param name="Status">
/// Always <see cref="OrderStatus.Submitted"/>. Every status after this one comes
/// from the read model, not from here.
/// </param>
/// <param name="Total">The order total, summed from its lines.</param>
public sealed record SubmitOrderResponse(
    Guid OrderId,
    OrderStatus Status,
    decimal Total);
