using Microsoft.Extensions.Time.Testing;
using Packly.Api.Features.Orders;
using Xunit;

namespace Packly.Api.Tests;

/// <summary>
/// Covers what the API accepts and what it refuses, which is the whole of its
/// contract for a caller who gets a request wrong.
/// </summary>
public sealed class SubmitOrderValidationTests
{
    private static readonly FakeTimeProvider Clock = new();

    [Fact]
    public void Request_EveryLineValid_BuildsOrderAndTotalsTheLines()
    {
        var request = Request(Item("MUG-1", "Mug", 2, 4.50m), Item("PEN-1", "Pen", 3, 1.20m));

        var built = SubmitOrderValidation.TryBuildOrder(request, Clock, out var order, out var errors);

        Assert.True(built);
        Assert.Empty(errors);
        Assert.Equal("ada", order.CustomerId);
        Assert.Equal(2, order.Items.Count);

        // Summed rather than taken from the request, so a caller cannot state a
        // total the lines disagree with.
        Assert.Equal(12.60m, order.Total);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CustomerId_MissingOrBlank_IsRefused(string? customerId)
    {
        var request = new SubmitOrderRequest(customerId, [Item("MUG-1", "Mug", 1, 4.50m)]);

        var built = SubmitOrderValidation.TryBuildOrder(request, Clock, out _, out var errors);

        Assert.False(built);
        Assert.Contains("CustomerId", errors.Keys);
    }

    [Fact]
    public void Items_Empty_IsRefused()
    {
        var request = new SubmitOrderRequest("ada", []);

        var built = SubmitOrderValidation.TryBuildOrder(request, Clock, out _, out var errors);

        Assert.False(built);
        Assert.Contains("Items", errors.Keys);
    }

    [Fact]
    public void Items_Null_IsRefused()
    {
        var request = new SubmitOrderRequest("ada", null);

        var built = SubmitOrderValidation.TryBuildOrder(request, Clock, out _, out var errors);

        Assert.False(built);
        Assert.Contains("Items", errors.Keys);
    }

    [Fact]
    public void Item_Null_IsReportedRatherThanDereferenced()
    {
        var request = new SubmitOrderRequest("ada", [null]);

        var built = SubmitOrderValidation.TryBuildOrder(request, Clock, out _, out var errors);

        Assert.False(built);
        Assert.Contains("Items[0]", errors.Keys);
    }

    [Theory]
    [InlineData(null, "Mug")]
    [InlineData("", "Mug")]
    [InlineData("   ", "Mug")]
    [InlineData("MUG-1", null)]
    [InlineData("MUG-1", "")]
    public void Item_BlankSkuOrName_IsRefused(string? sku, string? name)
    {
        var request = Request(new SubmitOrderItem(sku, name, 1, 4.50m));

        var built = SubmitOrderValidation.TryBuildOrder(request, Clock, out _, out var errors);

        Assert.False(built);
        Assert.Contains("Items[0]", errors.Keys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(null)]
    public void Item_QuantityNotAboveZero_IsRefused(int? quantity)
    {
        var request = Request(new SubmitOrderItem("MUG-1", "Mug", quantity, 4.50m));

        var built = SubmitOrderValidation.TryBuildOrder(request, Clock, out _, out var errors);

        Assert.False(built);
        Assert.Contains("Items[0]", errors.Keys);
    }

    [Fact]
    public void Item_NegativeUnitPrice_IsRefused()
    {
        var request = Request(Item("MUG-1", "Mug", 1, -0.01m));

        var built = SubmitOrderValidation.TryBuildOrder(request, Clock, out _, out var errors);

        Assert.False(built);
        Assert.Contains("Items[0]", errors.Keys);
    }

    [Fact]
    public void Item_ZeroUnitPrice_IsAcceptedBecauseZeroIsNotNegative()
    {
        var request = Request(Item("GIFT-1", "Gift", 1, 0m));

        var built = SubmitOrderValidation.TryBuildOrder(request, Clock, out var order, out _);

        Assert.True(built);
        Assert.Equal(0m, order.Total);
    }

    [Theory]
    [InlineData(1.005)]
    [InlineData(0.001)]
    public void Item_UnitPriceBeyondTwoDecimals_IsRefusedRatherThanRounded(decimal unitPrice)
    {
        // Rounding would answer with a total the stored order does not add up to:
        // the column is decimal(18,2) and SQL Server rounds on the way in.
        var request = Request(Item("MUG-1", "Mug", 1, unitPrice));

        var built = SubmitOrderValidation.TryBuildOrder(request, Clock, out _, out var errors);

        Assert.False(built);
        Assert.Contains("2 decimal places", string.Join(" ", errors["Items[0]"]), StringComparison.Ordinal);
    }

    [Fact]
    public void Item_UnitPriceAtTwoDecimals_IsAccepted()
    {
        var request = Request(Item("MUG-1", "Mug", 1, 4.99m));

        var built = SubmitOrderValidation.TryBuildOrder(request, Clock, out _, out _);

        Assert.True(built);
    }

    [Fact]
    public void Request_SeveralProblems_CollectsEveryOneNotOnlyTheFirst()
    {
        var request = new SubmitOrderRequest(
            null,
            [Item(string.Empty, string.Empty, 0, -1m), Item("PEN-1", "Pen", 1, 1.20m), null]);

        var built = SubmitOrderValidation.TryBuildOrder(request, Clock, out _, out var errors);

        // Validation runs to the end rather than returning early, so a caller
        // needs one round trip rather than one per mistake.
        Assert.False(built);
        Assert.Contains("CustomerId", errors.Keys);
        Assert.Contains("Items[0]", errors.Keys);
        Assert.Contains("Items[2]", errors.Keys);

        // The good line in the middle is not blamed for its neighbours.
        Assert.DoesNotContain("Items[1]", errors.Keys);

        // Four separate faults on the one bad line, reported together.
        Assert.Equal(4, errors["Items[0]"].Length);
    }

    private static SubmitOrderRequest Request(params SubmitOrderItem?[] items) =>
        new("ada", items);

    private static SubmitOrderItem Item(string sku, string name, int quantity, decimal unitPrice) =>
        new(sku, name, quantity, unitPrice);
}
