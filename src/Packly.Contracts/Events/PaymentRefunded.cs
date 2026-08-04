namespace Packly.Contracts.Events;

/// <summary>
/// A previously authorised payment was reversed, completing the compensation
/// for an order that could not be fulfilled.
/// </summary>
/// <param name="OrderId">The order that was refunded.</param>
/// <param name="PaymentReference">The authorisation that was reversed.</param>
/// <param name="Amount">The amount returned.</param>
public sealed record PaymentRefunded(
    Guid OrderId,
    string PaymentReference,
    decimal Amount);
