namespace Packly.Contracts.Events;

/// <summary>Funds were successfully authorised for an order.</summary>
/// <param name="OrderId">The order the funds were authorised for.</param>
/// <param name="PaymentReference">
/// Handle for the authorisation, needed later to refund it.
/// </param>
/// <param name="Amount">The amount authorised.</param>
public sealed record PaymentAuthorized(
    Guid OrderId,
    string PaymentReference,
    decimal Amount);
