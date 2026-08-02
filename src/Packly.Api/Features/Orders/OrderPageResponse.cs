namespace Packly.Api.Features.Orders;

/// <summary>
/// A page of orders, with enough context to ask for the next one.
/// </summary>
/// <param name="Items">The orders on this page, most recently updated first.</param>
/// <param name="Page">The page number returned, starting at 1.</param>
/// <param name="PageSize">How many orders a full page holds.</param>
/// <param name="Total">
/// How many orders match the filter in total. Returned so a caller can size a
/// pager without walking to the end, at the cost of a second count query.
/// </param>
public sealed record OrderPageResponse(
    IReadOnlyList<OrderSummaryResponse> Items,
    int Page,
    int PageSize,
    long Total);
