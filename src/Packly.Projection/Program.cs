using MassTransit;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Packly.Contracts;
using Packly.Messaging;
using Packly.Projection;

var builder = Host.CreateApplicationBuilder(args);

// Guids as the standard BSON binary subtype rather than the driver's legacy
// layout. Without this the serializer refuses to write one at all, because the
// representation is unspecified by default and it will not guess.
BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

// Field names as camelCase, so a document read in mongosh looks like what the
// API will later serve rather than like the C# class behind it.
ConventionRegistry.Register(
    "camelCase",
    new ConventionPack { new CamelCaseElementNameConvention() },
    _ => true);

var readModelUrl = new MongoUrl(
    builder.Configuration.GetConnectionString("ReadModel")
    ?? throw new InvalidOperationException("Connection string 'ReadModel' is required."));

builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(readModelUrl));
builder.Services.AddSingleton(provider => provider
    .GetRequiredService<IMongoClient>()
    .GetDatabase(readModelUrl.DatabaseName)
    .GetCollection<OrderStatusDocument>("order_status"));

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

await builder.Build().RunAsync();
