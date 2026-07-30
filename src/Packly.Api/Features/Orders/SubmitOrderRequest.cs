namespace Packly.Api.Features.Orders;

/// <summary>
/// The body of a request to place an order.
/// </summary>
/// <remarks>
/// Deliberately not the same type as the messages in Packly.Contracts. The HTTP
/// API and the message bus are two different public surfaces with two different
/// sets of consumers; tying them together means a change made for a browser
/// ripples out to every service on the bus.
/// </remarks>
/// <param name="CustomerId">Who is placing the order.</param>
/// <param name="Items">
/// The lines being ordered; at least one is required. The element type is
/// nullable because a JSON array can contain nulls and the deserialiser passes
/// them through untouched.
/// </param>
public sealed record SubmitOrderRequest(
    string? CustomerId,
    IReadOnlyList<SubmitOrderItem?>? Items);
