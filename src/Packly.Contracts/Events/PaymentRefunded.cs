namespace Packly.Contracts.Events;

/// <summary>
/// A previously authorised payment was reversed, completing the compensation
/// for an order that could not be fulfilled.
/// </summary>
public sealed record PaymentRefunded(
    Guid OrderId,
    string PaymentReference,
    decimal Amount);
