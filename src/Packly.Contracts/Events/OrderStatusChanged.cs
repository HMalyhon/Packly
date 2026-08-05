namespace Packly.Contracts.Events;

/// <summary>
/// An order moved to a new status. Published by the orchestrator on every saga
/// transition, and the single event that drives everything a user can observe.
/// </summary>
/// <param name="OrderId">The order whose status changed.</param>
/// <param name="Status">The status the order moved to.</param>
/// <param name="Version">
/// Monotonically increasing per order, starting at 1. What the projection
/// compares against to discard a stale delivery.
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
