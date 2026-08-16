using Packly.Payment;
using Xunit;

namespace Packly.Workers.Tests;

/// <summary>
/// Covers the rule the README documents with a runnable example.
/// </summary>
public sealed class AuthorizationRuleTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(4.50)]
    [InlineData(999.98)]
    [InlineData(999.99)]
    public void Amount_BelowTheThreshold_IsAuthorized(decimal amount) =>
        Assert.False(AuthorizationRule.IsDeclined(amount));

    // The threshold itself declines: the rule is >=, and the README's chair example
    // only works because 2 x 600 crosses it rather than equalling it.
    [Theory]
    [InlineData(1000)]
    [InlineData(1000.01)]
    [InlineData(1200)]
    public void Amount_AtOrAboveTheThreshold_IsDeclined(decimal amount) =>
        Assert.True(AuthorizationRule.IsDeclined(amount));

    [Fact]
    public void DeclineReason_AtTheThreshold_DoesNotClaimTheAmountExceededIt()
    {
        var reason = AuthorizationRule.DeclineReason(AuthorizationRule.DeclineThreshold);

        Assert.Contains("1000.00", reason, StringComparison.Ordinal);
        Assert.DoesNotContain("exceeds", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReferenceFor_SameOrder_IsAlwaysTheSame()
    {
        var orderId = Guid.CreateVersion7();

        // What makes a redelivery harmless: the retry authorises against the
        // reference the saga already holds.
        Assert.Equal(AuthorizationRule.ReferenceFor(orderId), AuthorizationRule.ReferenceFor(orderId));
    }

    [Fact]
    public void ReferenceFor_DifferentOrders_Differ() =>
        Assert.NotEqual(
            AuthorizationRule.ReferenceFor(Guid.CreateVersion7()),
            AuthorizationRule.ReferenceFor(Guid.CreateVersion7()));

    // Order ids are version 7, so 200 of them share a millisecond prefix. Truncating
    // the id instead of hashing it collapsed that batch to a single reference.
    [Fact]
    public void ReferenceFor_OrdersCreatedInTheSameMillisecond_StillDiffer()
    {
        var references = Enumerable
            .Range(0, 200)
            .Select(_ => AuthorizationRule.ReferenceFor(Guid.CreateVersion7()))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(200, references.Count);
    }

    [Fact]
    public void ReferenceFor_IsSixteenUppercaseCharacters()
    {
        var reference = AuthorizationRule.ReferenceFor(Guid.CreateVersion7());

        Assert.StartsWith("PAY-", reference, StringComparison.Ordinal);
        Assert.Equal(16, reference.Length);
        Assert.Equal(reference.ToUpperInvariant(), reference, StringComparer.Ordinal);
    }
}
