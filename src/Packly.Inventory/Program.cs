using MassTransit;
using Packly.Contracts;
using Packly.Inventory;
using Packly.Messaging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddMassTransit(bus =>
{
    bus.AddConsumer<ReserveStockConsumer>();
    bus.AddConsumer<PackOrderConsumer>();

    bus.UsingRabbitMq((context, rabbit) =>
    {
        rabbit.ConfigurePacklyHost(builder.Configuration);

        // Both consumers share one endpoint because the orchestrator addresses this
        // service by a single queue name.
        rabbit.ReceiveEndpoint(QueueNames.Inventory, endpoint =>
        {
            // Order matters here. UseInMemoryOutbox holds anything the consumer
            // publishes until it returns successfully, so a retried attempt cannot
            // leave a StockReserved behind from the attempt that failed. Retry
            // without it turns one transient fault into duplicate events.
            endpoint.UseInMemoryOutbox(context);
            endpoint.UseMessageRetry(retry => retry.Interval(3, TimeSpan.FromMilliseconds(200)));

            endpoint.ConfigureConsumer<ReserveStockConsumer>(context);
            endpoint.ConfigureConsumer<PackOrderConsumer>(context);
        });
    });
});

await builder.Build().RunAsync();
