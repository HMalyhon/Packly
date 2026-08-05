namespace Packly.Api.Features.Orders;

/// <summary>
/// A page of orders, with enough context to ask for the next one.
/// </summary>
/// <param name="Items">The orders on this page, most recently updated first.</param>
/// <param name="Page">The page number returned, starting at 1.</param>
/// <param name="PageSize">How many orders a full page holds.</param>
/// <param name="Total">Total matching the filter, at the cost of a second query.</param>
public sealed record OrderPageResponse(
    IReadOnlyList<OrderSummaryResponse> Items,
    int Page,
    int PageSize,
    long Total);
