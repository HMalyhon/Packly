namespace Packly.Contracts.Commands;

/// <summary>
/// Instructs the inventory service to reserve every line of an order.
/// Reservation is all-or-nothing: a single unavailable line fails the command.
/// </summary>
public sealed record ReserveStock(
    Guid OrderId,
    IReadOnlyList<OrderLine> Lines);
