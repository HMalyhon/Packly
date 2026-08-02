using Microsoft.AspNetCore.Http.HttpResults;
using MongoDB.Driver;
using Packly.ReadModel;

namespace Packly.Api.Features.Orders;

/// <summary>
/// Serves one order's status from the read model.
/// </summary>
public static class GetOrderStatusEndpoint
{
    /// <summary>
    /// Maps the single order query.
    /// </summary>
    /// <param name="app">The route builder to map onto.</param>
    public static void MapGetOrderStatus(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/orders/{orderId:guid}", HandleAsync)
            .WithName("GetOrderStatus")
            .WithSummary("Look up an order's status and history.")
            .WithDescription(
                "Answered from the read model, never from the tables that accept orders. " +
                "The read model is updated by an event after the fact, so an order submitted " +
                "a moment ago can still be missing here and answer 404 until the projection " +
                "catches up.")
            .WithTags("Orders");
    }

    private static async Task<Results<Ok<OrderStatusResponse>, NotFound>> HandleAsync(
        Guid orderId,
        IMongoCollection<OrderStatusDocument> collection,
        CancellationToken cancellationToken)
    {
        // A lookup by primary key. Everything the answer needs is in the one
        // document, which is what the projection exists to arrange.
        var document = await collection
            .Find(candidate => candidate.OrderId == orderId)
            .FirstOrDefaultAsync(cancellationToken);

        return document is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(ToResponse(document));
    }

    private static OrderStatusResponse ToResponse(OrderStatusDocument document) =>
        new(
            document.OrderId,
            document.Status,
            document.Description,
            document.Version,
            document.UpdatedAt,
            [.. document.History.Select(step => new OrderStatusStep(
                step.Status,
                step.Description,
                step.OccurredAt))]);
}
