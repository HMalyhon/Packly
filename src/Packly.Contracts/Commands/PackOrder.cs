namespace Packly.Contracts.Commands;

/// <summary>
/// Instructs the inventory service to pick and pack an order whose stock is
/// already reserved.
/// </summary>
public sealed record PackOrder(
    Guid OrderId,
    IReadOnlyList<OrderLine> Lines);
