using MassTransit;
using MongoDB.Driver;
using Packly.Contracts;
using Packly.Messaging;
using Packly.Projection;
using Packly.ReadModel;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddPacklyReadModel(builder.Configuration);

builder.Services.AddMassTransit(bus =>
{
    bus.AddConsumer<OrderStatusChangedConsumer>();

    bus.UsingRabbitMq((context, rabbit) =>
    {
        rabbit.ConfigurePacklyHost(builder.Configuration);

        // The second subscriber to the same event. Its queue is bound alongside
        // the notification service's rather than instead of it, so both receive
        // every status change and neither knows the other is listening.
        rabbit.ReceiveEndpoint(QueueNames.Projection, endpoint =>
        {
            // Worth retrying rather than dead-lettering: the write is idempotent,
            // so a second attempt after a transient database failure either
            // applies the change or discards it as superseded.
            endpoint.UseMessageRetry(retry => retry.Interval(3, TimeSpan.FromMilliseconds(200)));

            endpoint.ConfigureConsumer<OrderStatusChangedConsumer>(context);
        });
    });
});

var host = builder.Build();

// Indexes are created by the service that owns the schema, not by the ones that
// query it. Same reasoning as the migrations the API and orchestrator apply at
// startup: one command has to bring the whole stack up.
await using (var scope = host.Services.CreateAsyncScope())
{
    var collection = scope.ServiceProvider.GetRequiredService<IMongoCollection<OrderStatusDocument>>();
    var stopping = host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;

    // Serves the list query when it is filtered by status, which is the shape that
    // would otherwise scan. Unfiltered, a compound index led by status cannot help
    // and the query still scans and sorts in memory - acceptable for a collection
    // this size, and the honest reason not to add a second index for it. Lookups
    // by id are already served by the primary key, which is the order id.
    var byStatus = new CreateIndexModel<OrderStatusDocument>(
        Builders<OrderStatusDocument>.IndexKeys
            .Ascending(document => document.Status)
            .Descending(document => document.UpdatedAt));

    await collection.Indexes.CreateOneAsync(byStatus, cancellationToken: stopping);
}

await host.RunAsync();
