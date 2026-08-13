using Packly.Api.Features.Orders;
using Packly.Contracts;
using Xunit;

namespace Packly.Api.Tests;

/// <summary>
/// Covers the paging bounds, including the overflow that once escaped as a 500.
/// </summary>
public sealed class ListOrdersQueryTests
{
    private const int DefaultPage = 1;
    private const int PageSize = 20;

    [Fact]
    public void Query_DefaultParameters_IsAcceptedAndSkipsNothing()
    {
        var pageSize = ListOrdersQuery.DefaultPageSize;

        var valid = ListOrdersQuery.TryValidate(null, DefaultPage, pageSize, out var skip, out var errors);

        Assert.True(valid);
        Assert.Equal(0, skip);
        Assert.Empty(errors);
    }

    [Fact]
    public void Skip_ThirdPage_CountsThePagesBefore()
    {
        const int page = 3;

        var valid = ListOrdersQuery.TryValidate(null, page, PageSize, out var skip, out _);

        Assert.True(valid);
        Assert.Equal(40, skip);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(ListOrdersQuery.MaxPageSize + 1)]
    public void PageSize_OutOfRange_IsRefused(int pageSize)
    {
        var valid = ListOrdersQuery.TryValidate(null, DefaultPage, pageSize, out _, out var errors);

        Assert.False(valid);
        Assert.Contains("pageSize", errors.Keys);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(ListOrdersQuery.MaxPageSize)]
    public void PageSize_AtEitherBound_IsAccepted(int pageSize)
    {
        var valid = ListOrdersQuery.TryValidate(null, DefaultPage, pageSize, out _, out _);

        Assert.True(valid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Page_BelowOne_IsRefused(int page)
    {
        var valid = ListOrdersQuery.TryValidate(null, page, PageSize, out _, out var errors);

        Assert.False(valid);
        Assert.Contains("page", errors.Keys);
    }

    [Fact]
    public void Status_UndefinedValue_IsRefusedRatherThanMatchingNothing()
    {
        // ?status=99 casts cleanly from its number, so without the check it binds
        // and then answers an empty page, implying there are no such orders.
        var status = (OrderStatus)99;

        var valid = ListOrdersQuery.TryValidate(status, DefaultPage, PageSize, out _, out var errors);

        Assert.False(valid);
        Assert.Contains("status", errors.Keys);
    }

    [Fact]
    public void Status_DefinedValue_IsAccepted()
    {
        var status = OrderStatus.Cancelled;

        var valid = ListOrdersQuery.TryValidate(status, DefaultPage, PageSize, out _, out _);

        Assert.True(valid);
    }

    [Fact]
    public void Page_WouldOverflowTheSkip_IsRefused()
    {
        // The regression. (21474838 - 1) * 100 is 2,147,483,700, fifty-three past
        // int.MaxValue, and it wrapped to a negative skip the driver rejected as
        // an unhandled 500 - from a handler whose signature says it returns a 400
        // or a page and nothing else.
        const int page = 21474838;

        var valid = ListOrdersQuery.TryValidate(null, page, ListOrdersQuery.MaxPageSize, out var skip, out var errors);

        Assert.False(valid);
        Assert.Contains("page", errors.Keys);
        Assert.True(skip >= 0, "a refused query must not hand back a wrapped skip");
    }

    [Fact]
    public void Page_LargestThatStillFits_IsAccepted()
    {
        // One page below the overflow, so the boundary is pinned from both sides
        // rather than only from the side that broke.
        const int page = 21474837;

        var valid = ListOrdersQuery.TryValidate(null, page, ListOrdersQuery.MaxPageSize, out var skip, out _);

        Assert.True(valid);
        Assert.Equal(2147483600, skip);
    }
}
