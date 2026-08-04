namespace Packly.Contracts.Events;

/// <summary>
/// An order was accepted and durably recorded by the write model.
/// Published by the API through the transactional outbox, so this event exists
/// if and only if the order row was committed.
/// </summary>
public sealed record OrderSubmitted(
    Guid OrderId,
    string CustomerId,
    IReadOnlyList<OrderLine> Lines,
    decimal Total);
