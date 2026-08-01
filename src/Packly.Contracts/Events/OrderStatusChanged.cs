namespace Packly.Contracts.Events;

/// <summary>
/// An order moved to a new status. Published by the orchestrator on every saga
/// transition, and the single event that drives everything a user can observe.
/// </summary>
/// <remarks>
/// Published rather than sent, so the orchestrator names no recipient: it states
/// what happened and is done. Because it is the only publisher, order status has
/// exactly one source of truth however many services come to read it.
/// </remarks>
/// <param name="OrderId">The order whose status changed.</param>
/// <param name="Status">The status the order moved to.</param>
/// <param name="Version">
/// Monotonically increasing per order, starting at 1. RabbitMQ guarantees delivery
/// but not ordering across redeliveries, so the projection compares this against the
/// version it has already stored and discards anything stale. That makes the
/// projection idempotent, which in turn makes at-least-once delivery safe.
/// </param>
/// <param name="Description">
/// Human-readable text intended for the customer, for example
/// "Your order is being packed".
/// </param>
/// <param name="OccurredAt">When the transition happened.</param>
public sealed record OrderStatusChanged(
    Guid OrderId,
    OrderStatus Status,
    int Version,
    string Description,
    DateTimeOffset OccurredAt);
