namespace Packly.Contracts.Commands;

/// <summary>
/// Instructs the payment service to reverse a previously authorised payment.
/// This is the compensating action for an order that cannot be fulfilled after
/// its funds were already taken.
/// </summary>
public sealed record RefundPayment(
    Guid OrderId,
    string PaymentReference,
    decimal Amount,
    string Reason);
