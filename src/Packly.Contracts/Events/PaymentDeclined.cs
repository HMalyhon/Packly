namespace Packly.Contracts.Events;

/// <summary>
/// Authorisation was refused. Nothing was charged, so no compensation is owed
/// and the order can be rejected outright.
/// </summary>
public sealed record PaymentDeclined(
    Guid OrderId,
    string Reason);
