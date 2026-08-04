using MassTransit;
using Packly.Messaging;
using Packly.Notification;

// This service's own endpoint. Nothing sends to it - it subscribes by message
// type - so the name is declared here rather than in the shared contracts.
const string QueueName = "packly-notification";

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMassTransit(bus =>
{
    bus.AddConsumer<OrderStatusChangedConsumer>();

    bus.UsingRabbitMq((context, rabbit) =>
    {
        rabbit.ConfigurePacklyHost(builder.Configuration);

        // A queue of its own, bound to the OrderStatusChanged exchange. That is
        // what makes this a genuine second subscriber rather than a competing
        // consumer: every subscriber gets its own copy, so adding this service
        // takes nothing away from the ones already listening.
        rabbit.ReceiveEndpoint(QueueName, endpoint =>
        {
            // No outbox, because this consumer publishes nothing. Retries are here
            // for the transport rather than the work: a genuine mail service would
            // also need to recognise a message it has already sent, which needs
            // storage this simulation deliberately does not have.
            endpoint.UseMessageRetry(retry => retry.Interval(3, TimeSpan.FromMilliseconds(200)));

            endpoint.ConfigureConsumer<OrderStatusChangedConsumer>(context);
        });
    });
});

await builder.Build().RunAsync();
