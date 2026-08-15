using System.Security.Cryptography;

namespace Packly.Payment;

/// <summary>
/// What the payment simulation decides, apart from how it is delivered.
/// </summary>
internal static class AuthorizationRule
{
    // Fixed rather than random, so the declined path can be triggered on demand.
    internal const decimal DeclineThreshold = 1000m;

    internal static bool IsDeclined(decimal amount) => amount >= DeclineThreshold;

    internal static string DeclineReason(decimal amount) =>
        $"Amount {amount:0.00} is at or above the {DeclineThreshold:0.00} authorization limit.";

    /// <summary>
    /// The handle a refund is issued against, derived from the order so a redelivery
    /// cannot mint a second one. Hashed rather than truncated: the order id is a
    /// version 7 GUID, and its leading characters are a millisecond timestamp.
    /// </summary>
    /// <param name="orderId">The order being paid for.</param>
    /// <returns>The same reference for every delivery of one order.</returns>
    internal static string ReferenceFor(Guid orderId) =>
        $"PAY-{Convert.ToHexString(SHA256.HashData(orderId.ToByteArray()))[..12]}";
}
