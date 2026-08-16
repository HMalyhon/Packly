using Packly.Contracts;
using Packly.Inventory;
using Xunit;

namespace Packly.Workers.Tests;

/// <summary>
/// Covers the rule the README documents with a runnable example.
/// </summary>
public sealed class StockRuleTests
{
    [Fact]
    public void EveryLineAvailable_ReturnsNothing() =>
        Assert.Null(StockRule.FirstUnavailable([Line("MUG-1"), Line("PEN-1")]));

    [Theory]
    [InlineData("SOLD-OUT")]
    [InlineData("SOLD-OUT-1")]
    [InlineData("sold-out-9")]
    [InlineData("Sold-Out-Vinyl")]
    public void SkuStartingWithThePrefix_IsUnavailableWhateverItsCase(string sku) =>
        Assert.NotNull(StockRule.FirstUnavailable([Line(sku)]));

    // Starts with, not contains: a sku that merely mentions it is a real product.
    [Theory]
    [InlineData("NOT-SOLD-OUT-1")]
    [InlineData("MUG-SOLD-OUT")]
    public void SkuMerelyContainingThePrefix_IsAvailable(string sku) =>
        Assert.Null(StockRule.FirstUnavailable([Line(sku)]));

    [Fact]
    public void UnavailableLineAfterAvailableOnes_IsStillFound()
    {
        var soldOut = StockRule.FirstUnavailable([Line("MUG-1"), Line("PEN-1"), Line("SOLD-OUT-9")]);

        Assert.NotNull(soldOut);
        Assert.Equal("SOLD-OUT-9", soldOut.Sku);
    }

    [Fact]
    public void SeveralUnavailableLines_ReportsTheFirst()
    {
        var soldOut = StockRule.FirstUnavailable([Line("SOLD-OUT-1"), Line("SOLD-OUT-2")]);

        Assert.NotNull(soldOut);
        Assert.Equal("SOLD-OUT-1", soldOut.Sku);
    }

    [Fact]
    public void NoLines_ReturnsNothing() =>
        Assert.Null(StockRule.FirstUnavailable([]));

    private static OrderLine Line(string sku) => new(sku, "Item", 1, 1.00m);
}
