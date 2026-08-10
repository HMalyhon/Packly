using MassTransit;
using Packly.Contracts;
using Packly.Inventory;
using Packly.Messaging;

var builder = Host.CreateApplicationBuilder(args);

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
            // Inside the retry, not around it: an outbox that wraps the retry
            // flushes the failed attempt's StockReserved along with the good one.
            endpoint.UseMessageRetry(retry => retry.Interval(3, TimeSpan.FromMilliseconds(200)));
            endpoint.UseInMemoryOutbox(context);

            endpoint.ConfigureConsumer<ReserveStockConsumer>(context);
            endpoint.ConfigureConsumer<PackOrderConsumer>(context);
        });
    });
});

await builder.Build().RunAsync();
