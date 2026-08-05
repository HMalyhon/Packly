namespace Packly.Api.Features.Orders;

/// <summary>
/// The body of a request to place an order. Deliberately not the message type in
/// Packly.Contracts: the HTTP API and the bus are two public surfaces with two
/// sets of consumers.
/// </summary>
/// <param name="CustomerId">Who is placing the order.</param>
/// <param name="Items">
/// The lines being ordered; at least one is required. Nullable elements because a
/// JSON array can contain nulls and the deserialiser passes them through.
/// </param>
public sealed record SubmitOrderRequest(
    string? CustomerId,
    IReadOnlyList<SubmitOrderItem?>? Items);
