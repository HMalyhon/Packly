using MassTransit;
using Packly.Contracts;
using Packly.Contracts.Commands;
using Packly.Contracts.Events;

namespace Packly.Orchestrator;

/// <summary>
/// The order workflow, in one place.
/// </summary>
/// <remarks>
/// <para>
/// Every decision about what happens next to an order is made here. The services
/// that carry the steps out will each know how to do one job and nothing about
/// the sequence they sit in, so the flow can be rearranged without touching them.
/// </para>
/// <para>
/// This service is also the only publisher of <see cref="OrderStatusChanged"/>,
/// so order status has exactly one source of truth however many services react
/// to it.
/// </para>
/// </remarks>
public sealed class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrderStateMachine"/> class.
    /// </summary>
    /// <param name="timeProvider">Clock used to stamp published transitions.</param>
    /// <param name="logger">Records each transition.</param>
    public OrderStateMachine(TimeProvider timeProvider, ILogger<OrderStateMachine> logger)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        InstanceState(instance => instance.CurrentState);

        // Correlating on OrderId means the saga instance and the order share an
        // identity, so nothing has to map between them.
        Event(() => OrderSubmitted, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => PaymentAuthorized, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => PaymentDeclined, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => StockReserved, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => OrderPacked, x => x.CorrelateById(context => context.Message.OrderId));

        Initially(
            When(OrderSubmitted)
                .Then(context =>
                {
                    context.Saga.CustomerId = context.Message.CustomerId;
                    context.Saga.Total = context.Message.Total;
                    context.Saga.Lines = [.. context.Message.Lines];
                    context.Saga.StatusVersion++;

                    logger.LogInformation(
                        "Order {OrderId} submitted for {Total}, requesting payment authorization",
                        context.Saga.CorrelationId,
                        context.Saga.Total);
                })
                .Publish(context => new OrderStatusChanged(
                    context.Saga.CorrelationId,
                    OrderStatus.Submitted,
                    context.Saga.StatusVersion,
                    "We have your order.",
                    timeProvider.GetUtcNow()))
                .Send(
                    new Uri($"queue:{QueueNames.Payment}"),
                    context => new AuthorizePayment(
                        context.Saga.CorrelationId,
                        context.Saga.CustomerId,
                        context.Saga.Total))
                .TransitionTo(AwaitingPayment));

        // Ignoring OrderSubmitted here matters: a second one for an order already
        // in flight is a duplicate, not a new order, and without this it would
        // fault as an unhandled event and dead-letter. Saying so explicitly keeps
        // correct behaviour from looking like an oversight.
        During(
            AwaitingPayment,
            Ignore(OrderSubmitted),
            When(PaymentAuthorized)
                .Then(context =>
                {
                    context.Saga.StatusVersion++;

                    logger.LogInformation(
                        "Order {OrderId} payment authorized",
                        context.Saga.CorrelationId);
                })
                .Publish(context => new OrderStatusChanged(
                    context.Saga.CorrelationId,
                    OrderStatus.PaymentAuthorized,
                    context.Saga.StatusVersion,
                    "Payment authorized.",
                    timeProvider.GetUtcNow()))
                .Send(
                    new Uri($"queue:{QueueNames.Inventory}"),
                    context => new ReserveStock(
                        context.Saga.CorrelationId,
                        context.Saga.Lines))
                .TransitionTo(AwaitingStock),
            When(PaymentDeclined)
                .Then(context =>
                {
                    context.Saga.StatusVersion++;

                    logger.LogInformation(
                        "Order {OrderId} rejected: {Reason}",
                        context.Saga.CorrelationId,
                        context.Message.Reason);
                })
                .Publish(context => new OrderStatusChanged(
                    context.Saga.CorrelationId,
                    OrderStatus.Rejected,
                    context.Saga.StatusVersion,
                    context.Message.Reason,
                    timeProvider.GetUtcNow()))
                .TransitionTo(Rejected));

        During(
            AwaitingStock,
            When(StockReserved)
                .Then(context =>
                {
                    context.Saga.StatusVersion++;

                    logger.LogInformation(
                        "Order {OrderId} stock reserved, packing",
                        context.Saga.CorrelationId);
                })

                // Two facts, so two status changes: the stock is reserved, and
                // packing has begun. Collapsing them into one would lose a step a
                // customer cares about, and it is the step the product is named for.
                .Publish(context => new OrderStatusChanged(
                    context.Saga.CorrelationId,
                    OrderStatus.StockReserved,
                    context.Saga.StatusVersion,
                    "All items reserved.",
                    timeProvider.GetUtcNow()))
                .Then(context => context.Saga.StatusVersion++)
                .Publish(context => new OrderStatusChanged(
                    context.Saga.CorrelationId,
                    OrderStatus.Packing,
                    context.Saga.StatusVersion,
                    "Your order is being packed.",
                    timeProvider.GetUtcNow()))
                .Send(
                    new Uri($"queue:{QueueNames.Inventory}"),
                    context => new PackOrder(
                        context.Saga.CorrelationId,
                        context.Saga.Lines))
                .TransitionTo(Packing));

        During(
            Packing,
            When(OrderPacked)
                .Then(context =>
                {
                    context.Saga.StatusVersion++;

                    logger.LogInformation(
                        "Order {OrderId} packed, tracking {TrackingNumber}",
                        context.Saga.CorrelationId,
                        context.Message.TrackingNumber);
                })
                .Publish(context => new OrderStatusChanged(
                    context.Saga.CorrelationId,
                    OrderStatus.Completed,
                    context.Saga.StatusVersion,
                    $"On its way. Tracking {context.Message.TrackingNumber}.",
                    timeProvider.GetUtcNow()))
                .TransitionTo(Completed));

        // An event for a step the order has already passed is a duplicate, and a
        // duplicate is not an error. The inbox only catches redeliveries of the
        // same MessageId; a worker that publishes twice - after a retry, say -
        // produces a fresh MessageId that reaches here as a genuinely new message.
        // Without these the saga throws UnhandledEventException, exhausts its
        // retries and dead-letters an order that is otherwise perfectly fine.
        During(
            AwaitingStock,
            Ignore(OrderSubmitted),
            Ignore(PaymentAuthorized));

        During(
            Packing,
            Ignore(OrderSubmitted),
            Ignore(PaymentAuthorized),
            Ignore(StockReserved));

        // Terminal states ignore everything: whatever arrives now, the answer has
        // already been given and published.
        During(
            Completed,
            Rejected,
            Ignore(OrderSubmitted),
            Ignore(PaymentAuthorized),
            Ignore(PaymentDeclined),
            Ignore(StockReserved),
            Ignore(OrderPacked));
    }

    /// <summary>
    /// Gets the state entered once payment has been asked for and not yet answered.
    /// </summary>
    public State AwaitingPayment { get; private set; } = null!;

    /// <summary>
    /// Gets the state entered once payment succeeded and stock has been requested.
    /// </summary>
    public State AwaitingStock { get; private set; } = null!;

    /// <summary>
    /// Gets the state entered while the order is being picked and packed.
    /// </summary>
    public State Packing { get; private set; } = null!;

    /// <summary>
    /// Gets the terminal state for an order that was packed and dispatched.
    /// </summary>
    public State Completed { get; private set; } = null!;

    /// <summary>
    /// Gets the terminal state for an order whose payment was refused.
    /// </summary>
    /// <remarks>
    /// Distinct from cancellation. Nothing was ever charged, so there is nothing
    /// to compensate and the order simply stops; an order cancelled after payment
    /// has to be refunded first.
    /// </remarks>
    public State Rejected { get; private set; } = null!;

    /// <summary>Gets the event raised when the API accepts a new order.</summary>
    public Event<OrderSubmitted> OrderSubmitted { get; private set; } = null!;

    /// <summary>Gets the event raised when funds were successfully authorised.</summary>
    public Event<PaymentAuthorized> PaymentAuthorized { get; private set; } = null!;

    /// <summary>Gets the event raised when authorisation was refused.</summary>
    public Event<PaymentDeclined> PaymentDeclined { get; private set; } = null!;

    /// <summary>Gets the event raised when every line was reserved from stock.</summary>
    public Event<StockReserved> StockReserved { get; private set; } = null!;

    /// <summary>Gets the event raised when the order has been packed for dispatch.</summary>
    public Event<OrderPacked> OrderPacked { get; private set; } = null!;
}
