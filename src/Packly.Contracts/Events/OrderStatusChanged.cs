namespace Packly.Contracts.Events;

/// <summary>
/// An order moved to a new status. Published by the orchestrator on every saga
/// transition, and the single event that drives everything a user can observe.
/// </summary>
/// <remarks>
/// The version is monotonic per order and starts at 1. It is what the projection
/// compares against to discard a delivery that arrived out of order, and the
/// description is the text the customer is shown.
/// </remarks>
public sealed record OrderStatusChanged(
    Guid OrderId,
    OrderStatus Status,
    int Version,
    string Description,
    DateTimeOffset OccurredAt);
