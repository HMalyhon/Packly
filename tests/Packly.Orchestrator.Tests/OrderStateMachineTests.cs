using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Packly.Contracts;
using Packly.Contracts.Commands;
using Packly.Contracts.Events;
using Xunit;

namespace Packly.Orchestrator.Tests;

/// <summary>
/// Covers the three ways an order can end, and the duplicate that must not end it.
/// </summary>
/// <remarks>
/// The saga is tested against an in-memory bus and repository rather than RabbitMQ
/// and SQL Server. What is under test is the decision - which command follows
/// which event, and which state the order lands in - and that logic is the same
/// whichever transport carries it.
/// </remarks>
public sealed class OrderStateMachineTests
{
    private const decimal Total = 9.00m;

    private static readonly OrderLine[] Lines = [new("MUG-1", "Mug", 2, 4.50m)];

    [Fact]
    public async Task OrderSubmitted_NewOrder_SendsAuthorizePayment()
    {
        await using var provider = BuildProvider();
        var harness = await StartAsync(provider);
        var orderId = NewOrderId();

        await harness.Bus.Publish(new OrderSubmitted(orderId, "ada", Lines, Total));

        Assert.True(await SagaOf(harness).Exists(orderId, x => x.AwaitingPayment) is not null);
        var authorize = await FirstSent<AuthorizePayment>(harness);
        Assert.Equal(orderId, authorize.OrderId);
        Assert.Equal(Total, authorize.Amount);
        Assert.Equal([OrderStatus.Submitted], await StatusesFor(harness, orderId));
    }

    [Fact]
    public async Task OrderPacked_AfterPaymentAndStock_CompletesWithGapFreeVersions()
    {
        await using var provider = BuildProvider();
        var harness = await StartAsync(provider);
        var orderId = NewOrderId();

        await harness.Bus.Publish(new OrderSubmitted(orderId, "ada", Lines, Total));
        await harness.Bus.Publish(new PaymentAuthorized(orderId, "PAY-1", Total));
        await harness.Bus.Publish(new StockReserved(orderId));
        await harness.Bus.Publish(new OrderPacked(orderId, "PK-1"));

        Assert.True(await SagaOf(harness).Exists(orderId, x => x.Completed) is not null);

        // The order the customer sees, and the reason the projection can discard a
        // stale delivery: one version per status, in order, with no gaps.
        Assert.Equal(
            [
                OrderStatus.Submitted,
                OrderStatus.PaymentAuthorized,
                OrderStatus.StockReserved,
                OrderStatus.Packing,
                OrderStatus.Completed,
            ],
            await StatusesFor(harness, orderId));
        Assert.Equal([1, 2, 3, 4, 5], await VersionsFor(harness, orderId));
    }

    [Fact]
    public async Task PaymentDeclined_NothingAuthorizedYet_RejectsWithoutReservingOrRefunding()
    {
        await using var provider = BuildProvider();
        var harness = await StartAsync(provider);
        var orderId = NewOrderId();

        await harness.Bus.Publish(new OrderSubmitted(orderId, "ada", Lines, Total));
        await harness.Bus.Publish(new PaymentDeclined(orderId, "Card refused."));

        Assert.True(await SagaOf(harness).Exists(orderId, x => x.Rejected) is not null);

        // Nothing was authorised, so nothing is owed back - the difference between
        // Rejected and Cancelled.
        Assert.False(await harness.Sent.Any<ReserveStock>());
        Assert.False(await harness.Sent.Any<RefundPayment>());
        Assert.Equal(
            [OrderStatus.Submitted, OrderStatus.Rejected],
            await StatusesFor(harness, orderId));
    }

    [Fact]
    public async Task StockUnavailable_AfterPaymentAuthorized_RefundsBeforeCancelling()
    {
        await using var provider = BuildProvider();
        var harness = await StartAsync(provider);
        var orderId = NewOrderId();

        await harness.Bus.Publish(new OrderSubmitted(orderId, "ada", Lines, Total));
        await harness.Bus.Publish(new PaymentAuthorized(orderId, "PAY-7", Total));
        await harness.Bus.Publish(new StockUnavailable(orderId, "SOLD-OUT-1", "out of stock"));

        // The compensation names the authorisation it reverses, which is the whole
        // reason the saga keeps the reference across several messages.
        var refund = await FirstSent<RefundPayment>(harness);
        Assert.Equal("PAY-7", refund.PaymentReference);
        Assert.Equal(Total, refund.Amount);
        Assert.Contains("SOLD-OUT-1", refund.Reason, StringComparison.Ordinal);
        Assert.True(await SagaOf(harness).Exists(orderId, x => x.Refunding) is not null);

        // Nothing is said to the customer until the money is actually back.
        Assert.DoesNotContain(OrderStatus.Cancelled, await StatusesFor(harness, orderId));

        await harness.Bus.Publish(new PaymentRefunded(orderId, "PAY-7", Total));

        Assert.True(await SagaOf(harness).Exists(orderId, x => x.Cancelled) is not null);
        Assert.Equal(
            [OrderStatus.Submitted, OrderStatus.PaymentAuthorized, OrderStatus.Cancelled],
            await StatusesFor(harness, orderId));
    }

    [Fact]
    public async Task DuplicateEvent_OrderAlreadyTerminal_IsIgnoredWithoutFaulting()
    {
        var logs = new CapturedLogs();
        await using var provider = BuildProvider(logs);
        var harness = await StartAsync(provider);
        var orderId = NewOrderId();

        await harness.Bus.Publish(new OrderSubmitted(orderId, "ada", Lines, Total));
        await harness.Bus.Publish(new PaymentAuthorized(orderId, "PAY-1", Total));
        await harness.Bus.Publish(new StockReserved(orderId));
        await harness.Bus.Publish(new OrderPacked(orderId, "PK-1"));

        Assert.True(await SagaOf(harness).Exists(orderId, x => x.Completed) is not null);

        var before = (await StatusesFor(harness, orderId)).Count;

        // A worker that crashes between publishing and acknowledging republishes
        // with a fresh message id, which the inbox cannot dedupe. It arrives here
        // as a new message about a step the order passed long ago.
        await harness.Bus.Publish(new StockReserved(orderId));
        await harness.Bus.Publish(new PaymentRefunded(orderId, "PAY-1", Total));

        Assert.False(await harness.Published.Any<Fault<StockReserved>>());
        Assert.False(await harness.Published.Any<Fault<PaymentRefunded>>());

        // Reached a decision rather than losing the message on the way: an ignored
        // event is recorded nowhere else, so the log line is the evidence. Waited
        // for rather than asserted outright - publishing returns before the saga
        // has read the message.
        //
        // The level is asserted, not incidental. Ignoring is deliberate but not
        // routine, and information is where a genuinely missing transition would
        // go unnoticed.
        Assert.True(await LoggedAsync(logs, LogLevel.Warning, "ignored StockReserved in state Completed"));
        Assert.True(await LoggedAsync(logs, LogLevel.Warning, "ignored PaymentRefunded in state Completed"));

        Assert.True(await SagaOf(harness).Exists(orderId, x => x.Completed) is not null);
        Assert.Equal(before, (await StatusesFor(harness, orderId)).Count);
    }

    private static async Task<bool> LoggedAsync(CapturedLogs logs, LogLevel level, string expected)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        while (!timeout.IsCancellationRequested)
        {
            if (logs.Entries.Any(entry =>
                entry.Level == level && entry.Message.Contains(expected, StringComparison.Ordinal)))
            {
                return true;
            }

            await Task.Delay(25, CancellationToken.None);
        }

        return false;
    }

    private static ServiceProvider BuildProvider(CapturedLogs? logs = null) =>
        new ServiceCollection()
            .AddSingleton<TimeProvider>(new FakeTimeProvider())
            .AddLogging(builder =>
            {
                if (logs is not null)
                {
                    builder.AddProvider(logs);
                }
            })
            .AddMassTransitTestHarness(bus =>
                bus.AddSagaStateMachine<OrderStateMachine, OrderState>()
                    .InMemoryRepository())
            .BuildServiceProvider(true);

    private static async Task<ITestHarness> StartAsync(ServiceProvider provider)
    {
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return harness;
    }

    private static ISagaStateMachineTestHarness<OrderStateMachine, OrderState> SagaOf(
        ITestHarness harness) =>
        harness.GetSagaStateMachineHarness<OrderStateMachine, OrderState>();

    // Version 7, like the API's, so ordering by id orders by submission.
    private static Guid NewOrderId() => Guid.CreateVersion7();

    private static async Task<T> FirstSent<T>(ITestHarness harness)
        where T : class
    {
        Assert.True(await harness.Sent.Any<T>());
        return harness.Sent.Select<T>().First().Context.Message;
    }

    private static async Task<IReadOnlyList<OrderStatus>> StatusesFor(
        ITestHarness harness,
        Guid orderId)
    {
        await harness.Published.Any<OrderStatusChanged>();

        return [.. StatusChangesFor(harness, orderId).Select(message => message.Status)];
    }

    private static async Task<IReadOnlyList<int>> VersionsFor(ITestHarness harness, Guid orderId)
    {
        await harness.Published.Any<OrderStatusChanged>();

        return [.. StatusChangesFor(harness, orderId).Select(message => message.Version)];
    }

    private static IEnumerable<OrderStatusChanged> StatusChangesFor(
        ITestHarness harness,
        Guid orderId) =>
        harness.Published
            .Select<OrderStatusChanged>()
            .Select(published => published.Context.Message)
            .Where(message => message.OrderId == orderId)
            .OrderBy(message => message.Version);
}
