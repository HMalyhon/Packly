namespace Packly.Contracts.Commands;

/// <summary>
/// Instructs the payment service to reverse a previously authorised payment.
/// This is the compensating action for an order that cannot be fulfilled after
/// its funds were already taken.
/// </summary>
/// <param name="OrderId">The order being refunded.</param>
/// <param name="PaymentReference">The authorisation being reversed.</param>
/// <param name="Amount">The amount to return.</param>
/// <param name="Reason">Why the order is being called off, for the record.</param>
public sealed record RefundPayment(
    Guid OrderId,
    string PaymentReference,
    decimal Amount,
    string Reason);
