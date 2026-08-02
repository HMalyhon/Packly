using MassTransit;
using Microsoft.EntityFrameworkCore;
using Packly.Contracts;
using Packly.Messaging;
using Packly.Orchestrator;
using Packly.Orchestrator.Persistence;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<OrderStateDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("OrderStateDb"),
        sql => sql.EnableRetryOnFailure()));

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddMassTransit(bus =>
{
    bus.AddSagaStateMachine<OrderStateMachine, OrderState>()
        .EntityFrameworkRepository(repository =>
        {
            // Optimistic rather than pessimistic: concurrent messages for a single
            // order are uncommon, and an occasional retry costs less than every
            // message taking a row lock. The rowversion column makes the loser
            // retry instead of overwriting the winner.
            repository.ConcurrencyMode = ConcurrencyMode.Optimistic;
            repository.ExistingDbContext<OrderStateDbContext>();
        });

    bus.AddEntityFrameworkOutbox<OrderStateDbContext>(outbox =>
    {
        outbox.UseSqlServer();
        outbox.UseBusOutbox();
    });

    bus.UsingRabbitMq((context, rabbit) =>
    {
        rabbit.ConfigurePacklyHost(builder.Configuration);

        rabbit.ReceiveEndpoint(QueueNames.Orchestrator, endpoint =>
        {
            // Required for optimistic concurrency to mean anything: MassTransit
            // does not retry on its own, so without this a losing write would
            // dead-letter the order instead of trying again. Also covers ordinary
            // transient database failures.
            endpoint.UseMessageRetry(retry => retry.Interval(5, TimeSpan.FromMilliseconds(200)));

            // Deduplicates redeliveries against the InboxState table, so
            // at-least-once delivery cannot drive the same transition twice.
            endpoint.UseEntityFrameworkOutbox<OrderStateDbContext>(context);

            endpoint.ConfigureSaga<OrderState>(context);
        });
    });
});

var host = builder.Build();

// Same reasoning as the API: this stack has to come up with one command. A real
// deployment would migrate as its own step.
await using (var scope = host.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<OrderStateDbContext>();
    await dbContext.Database.MigrateAsync();
}

await host.RunAsync();
