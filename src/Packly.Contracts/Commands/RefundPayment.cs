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
public sealed record RefundPayment(
    Guid OrderId,
    string PaymentReference,
    decimal Amount,
    string Reason);
