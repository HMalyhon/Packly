using MassTransit;
using Microsoft.AspNetCore.Http.HttpResults;
using Packly.Api.Domain;
using Packly.Api.Persistence;
using Packly.Contracts;
using Packly.Contracts.Events;

namespace Packly.Api.Features.Orders;

/// <summary>
/// Accepts new orders onto the write side.
/// </summary>
public static class SubmitOrderEndpoint
{
    /// <summary>
    /// Maps the order submission endpoint.
    /// </summary>
    /// <param name="app">The route builder to map onto.</param>
    public static void MapSubmitOrder(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost("/api/orders", HandleAsync)
            .WithName("SubmitOrder")
            .WithSummary("Place an order.")
            .WithDescription(
                "Records the order and returns immediately. Payment, stock and packing " +
                "happen afterwards on other services, so the status returned here is the " +
                "starting point rather than the outcome.")
            .WithTags("Orders");
    }

    private static async Task<Results<Accepted<SubmitOrderResponse>, ValidationProblem>> HandleAsync(
        SubmitOrderRequest request,
        OrdersDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        TimeProvider timeProvider,
        ILogger<Order> logger,
        CancellationToken cancellationToken)
    {
        if (!SubmitOrderValidation.TryBuildOrder(request, timeProvider, out var order, out var errors))
        {
            return TypedResults.ValidationProblem(errors);
        }

        dbContext.Orders.Add(order);

        // Publishing before SaveChanges is not a mistake. With the bus outbox
        // configured, this does not touch RabbitMQ: it stages a row in the same
        // DbContext, so the line below commits the order and the event together or
        // commits neither. Delivery to the broker happens afterwards, and retries
        // until it succeeds.
        await publishEndpoint.Publish(ToEvent(order), cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Order {OrderId} submitted by {CustomerId} for {Total}",
            order.Id,
            order.CustomerId,
            order.Total);

        // 202 rather than 201: the order is accepted, and the work it triggers has
        // not happened yet. A 201 would imply the resource is in its final state.
        return TypedResults.Accepted(
            $"/api/orders/{order.Id}",
            new SubmitOrderResponse(order.Id, OrderStatus.Submitted, order.Total));
    }

    /// <summary>
    /// Translates the aggregate into the event other services consume.
    /// </summary>
    /// <remarks>
    /// Written out by hand rather than mapped automatically. The event is a
    /// published contract: it should change when someone decides it should, not
    /// because a property was renamed on an internal type.
    /// </remarks>
    private static OrderSubmitted ToEvent(Order order) =>
        new(
            order.Id,
            order.CustomerId,
            [.. order.Items.Select(item => new OrderLine(item.Sku, item.Name, item.Quantity, item.UnitPrice))],
            order.Total);
}
