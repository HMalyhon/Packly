using MassTransit;
using Microsoft.EntityFrameworkCore;
using Packly.Messaging;
using Packly.Orchestrator;
using Packly.Orchestrator.Persistence;

// This service's own endpoint. Nothing sends to it - it subscribes by message
// type - so the name is declared here rather than in the shared contracts.
const string QueueName = "packly-orchestrator";

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<OrderStateDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("OrderStateDb"),
        sql => sql.EnableRetryOnFailure()));

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddPacklyTelemetry("packly-orchestrator");

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

        rabbit.ReceiveEndpoint(QueueName, endpoint =>
        {
            // Two jobs, so one policy that grows. The first retry is near
            // immediate, which is all an optimistic concurrency loser needs -
            // MassTransit does not retry on its own, so without it a losing write
            // dead-letters the order. Each subsequent wait adds two seconds,
            // reaching about ninety in total, which is what a database restart
            // needs: five attempts at 200ms gave up after a second, and an event
            // arriving during one was dead-lettered, stranding an otherwise
            // healthy order in whatever state it was passing through.
            endpoint.UseMessageRetry(retry => retry.Incremental(
                retryLimit: 10,
                initialInterval: TimeSpan.FromMilliseconds(200),
                intervalIncrement: TimeSpan.FromSeconds(2)));

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
