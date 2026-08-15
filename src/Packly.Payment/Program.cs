using MassTransit;
using Packly.Contracts;
using Packly.Messaging;
using Packly.Payment;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddPacklyTelemetry("packly-payment");

builder.Services.AddMassTransit(bus =>
{
    bus.AddConsumer<AuthorizePaymentConsumer>();
    bus.AddConsumer<RefundPaymentConsumer>();

    bus.UsingRabbitMq((context, rabbit) =>
    {
        rabbit.ConfigurePacklyHost(builder.Configuration);

        // Named explicitly rather than derived from the consumer type, because the
        // orchestrator addresses this queue by name when it sends a command. The
        // constant is shared so a rename cannot silently break the send.
        rabbit.ReceiveEndpoint(QueueNames.Payment, endpoint =>
        {
            // Retry first, so the outbox sits inside it rather than around it.
            // UsePacklyRetry spells out why that order is the whole point.
            endpoint.UsePacklyRetry();
            endpoint.UseInMemoryOutbox(context);

            endpoint.ConfigureConsumer<AuthorizePaymentConsumer>(context);
            endpoint.ConfigureConsumer<RefundPaymentConsumer>(context);
        });
    });
});

await builder.Build().RunAsync();
