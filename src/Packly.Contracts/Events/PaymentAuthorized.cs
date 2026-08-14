namespace Packly.Contracts.Events;

/// <summary>
/// Funds were successfully authorised for an order. The reference is the handle
/// a refund is later issued against.
/// </summary>
public sealed record PaymentAuthorized(
    Guid OrderId,
    string PaymentReference,
    decimal Amount);
