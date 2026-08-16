using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Packly.Messaging;
using Xunit;

namespace Packly.Workers.Tests;

/// <summary>
/// Pins the ordering the payment and inventory endpoints depend on: the outbox is
/// configured inside the retry, so an attempt that fails publishes nothing.
/// </summary>
/// <remarks>
/// The consumer here is a stand-in rather than a real one - what is under test is
/// the pipe MassTransit builds from the configuration order, not the work.
/// </remarks>
public sealed class OutboxOrderingTests
{
    [Fact]
    public async Task OutboxInsideRetry_PublishesOnlyFromTheAttemptThatSucceeded()
    {
        await using var provider = Harness(outboxInsideRetry: true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(new WorkRequested(Guid.NewGuid()));
        await harness.InactivityTask;

        Assert.Equal(2, provider.GetRequiredService<Attempts>().Count);
        Assert.Single(harness.Published.Select<WorkFinished>());
    }

    // The same pieces in the other order, to show the ordering is what matters
    // rather than the outbox merely being present.
    [Fact]
    public async Task OutboxAroundRetry_AlsoFlushesTheAttemptThatFailed()
    {
        await using var provider = Harness(outboxInsideRetry: false);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(new WorkRequested(Guid.NewGuid()));
        await harness.InactivityTask;

        Assert.Equal(2, provider.GetRequiredService<Attempts>().Count);
        Assert.Equal(2, harness.Published.Select<WorkFinished>().Count());
    }

    private static ServiceProvider Harness(bool outboxInsideRetry) =>
        new ServiceCollection()
            .AddSingleton<Attempts>()
            .AddMassTransitTestHarness(bus =>
            {
                bus.AddConsumer<FlakyConsumer>();

                bus.UsingInMemory((context, cfg) =>
                    cfg.ReceiveEndpoint("work", endpoint =>
                    {
                        if (outboxInsideRetry)
                        {
                            endpoint.UsePacklyRetry();
                            endpoint.UseInMemoryOutbox(context);
                        }
                        else
                        {
                            endpoint.UseInMemoryOutbox(context);
                            endpoint.UsePacklyRetry();
                        }

                        endpoint.ConfigureConsumer<FlakyConsumer>(context);
                    }));
            })
            .BuildServiceProvider(true);

    public sealed record WorkRequested(Guid Id);

    public sealed record WorkFinished(Guid Id);

    public sealed class Attempts
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public bool IsFirst() => Interlocked.Increment(ref _count) == 1;
    }

    public sealed class FlakyConsumer(Attempts attempts) : IConsumer<WorkRequested>
    {
        public async Task Consume(ConsumeContext<WorkRequested> context)
        {
            ArgumentNullException.ThrowIfNull(context);

            await context.Publish(new WorkFinished(context.Message.Id), context.CancellationToken);

            if (attempts.IsFirst())
            {
                throw new InvalidOperationException("transient failure");
            }
        }
    }
}
