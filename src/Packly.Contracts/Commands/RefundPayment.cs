namespace Packly.Contracts.Commands;

/// <summary>
/// Instructs the payment service to reverse a previously authorised payment.
/// This is the compensating action for an order that cannot be fulfilled after
/// its payment was already authorised.
/// </summary>
/// <remarks>
/// Authorisation is all this workflow performs, so the reversal is strictly a
/// release of held funds rather than the return of captured ones. A system that
/// captured at dispatch would compensate differently before and after that point,
/// which is why the distinction is worth keeping even in a simulation.
/// </remarks>
/// <param name="OrderId">The order being refunded.</param>
/// <param name="PaymentReference">The authorisation being reversed.</param>
/// <param name="Amount">The amount to return.</param>
/// <param name="Reason">Why the order is being called off, for the record.</param>
public sealed record RefundPayment(
    Guid OrderId,
    string PaymentReference,
    decimal Amount,
    string Reason);
