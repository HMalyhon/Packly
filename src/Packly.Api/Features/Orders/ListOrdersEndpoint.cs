using Microsoft.AspNetCore.Http.HttpResults;
using MongoDB.Driver;
using Packly.Contracts;
using Packly.ReadModel;

namespace Packly.Api.Features.Orders;

/// <summary>
/// Serves a page of orders from the read model.
/// </summary>
/// <remarks>
/// The query the write model would answer badly. Filtering orders by their current
/// status against the write side would mean deriving that status from the saga or
/// from an event log on every request; here it is a stored field, and when the
/// caller supplies one it is served by an index rather than a scan.
/// </remarks>
public static class ListOrdersEndpoint
{
    /// <summary>
    /// Maps the order list query.
    /// </summary>
    /// <param name="app">The route builder to map onto.</param>
    public static void MapListOrders(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/orders", HandleAsync)
            .WithName("ListOrders")
            .WithSummary("List orders, most recently updated first.")
            .WithDescription(
                "Answered from the read model. Optionally filtered by current status, " +
                "which is a stored field here rather than something the write side would " +
                "have to derive per request.")
            .WithTags("Orders");
    }

    private static async Task<Results<Ok<OrderPageResponse>, ValidationProblem>> HandleAsync(
        IMongoCollection<OrderStatusDocument> collection,
        CancellationToken cancellationToken,
        OrderStatus? status = null,
        int page = 1,
        int pageSize = ListOrdersQuery.DefaultPageSize)
    {
        if (!ListOrdersQuery.TryValidate(status, page, pageSize, out var skip, out var errors))
        {
            return TypedResults.ValidationProblem(errors);
        }

        var filter = status is null
            ? Builders<OrderStatusDocument>.Filter.Empty
            : Builders<OrderStatusDocument>.Filter.Eq(document => document.Status, status.Value);

        // Counted separately from the page itself, so the caller can size a pager.
        // The two run against a collection that is still being written to, so a
        // total can disagree with the page by an order that arrived in between -
        // acceptable for a listing, and cheaper than a transaction to prevent it.
        var total = await collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        var documents = await collection
            .Find(filter)
            .SortByDescending(document => document.UpdatedAt)
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new OrderPageResponse(
            [.. documents.Select(document => new OrderSummaryResponse(
                document.OrderId,
                document.Status,
                document.Description,
                document.UpdatedAt))],
            page,
            pageSize,
            total));
    }
}
