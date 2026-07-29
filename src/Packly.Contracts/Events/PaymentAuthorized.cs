namespace Packly.Contracts.Events;

/// <summary>Funds were successfully authorised for an order.</summary>
/// <param name="PaymentReference">
/// Handle for the authorisation, needed later to refund it.
/// </param>
public sealed record PaymentAuthorized(
    Guid OrderId,
    string PaymentReference,
    decimal Amount,
    DateTimeOffset AuthorizedAt);
