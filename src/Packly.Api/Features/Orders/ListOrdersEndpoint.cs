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
    private const int DefaultPageSize = 20;

    // A cap rather than a suggestion: without one a single request can ask the
    // database for the whole collection and the API will serialise all of it.
    private const int MaxPageSize = 100;

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
        int pageSize = DefaultPageSize)
    {
        var errors = new Dictionary<string, string[]>();

        if (pageSize is < 1 or > MaxPageSize)
        {
            errors[nameof(pageSize)] = [$"Page size must be between 1 and {MaxPageSize}."];
        }

        // Widened before multiplying, because the product of two valid ints is not
        // necessarily one. page=21474838 with a page size of 100 wrapped to a
        // negative skip, which the driver rejected as an unhandled 500 - a bad
        // request escaping as a server error this signature says cannot happen.
        var skip = ((long)page - 1) * pageSize;

        if (page < 1 || skip > int.MaxValue)
        {
            errors[nameof(page)] = ["Page must be 1 or greater, and within range for the page size."];
        }

        // An undefined value casts cleanly from its number, so ?status=99 binds
        // without complaint and then matches nothing. Reported as the bad request
        // it is rather than answered with an empty page that implies there are no
        // such orders.
        if (status is not null && !Enum.IsDefined(status.Value))
        {
            errors[nameof(status)] = ["Unknown order status."];
        }

        if (errors.Count > 0)
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
            .Skip((int)skip)
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
