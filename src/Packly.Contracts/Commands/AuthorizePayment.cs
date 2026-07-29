namespace Packly.Contracts.Commands;

/// <summary>
/// Instructs the payment service to authorise funds for an order.
/// Sent by the orchestrator when an order enters the payment stage.
/// </summary>
public sealed record AuthorizePayment(
    Guid OrderId,
    string CustomerId,
    decimal Amount);
