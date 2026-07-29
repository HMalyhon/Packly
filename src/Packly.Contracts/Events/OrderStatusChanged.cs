namespace Packly.Contracts.Events;

/// <summary>
/// An order moved to a new status. Published by the orchestrator on every saga
/// transition, and the single event that drives everything a user can observe.
/// </summary>
/// <remarks>
/// <para>
/// Three unrelated services subscribe to this one event, which is what makes the
/// publish/subscribe fan-out concrete rather than theoretical:
/// the projection service upserts the read model, the notification service reacts
/// to terminal statuses, and the API pushes it to connected browsers over SignalR.
/// None of them know the others exist.
/// </para>
/// <para>
/// Because the orchestrator is the only publisher, order status has exactly one
/// source of truth even though it is read from several places.
/// </para>
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
