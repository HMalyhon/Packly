using MassTransit;
using Packly.Contracts;
using Packly.Messaging;
using Packly.Payment;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);

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
            // Outermost, so it wraps the retry rather than the other way round:
            // publishes are held until the consumer returns successfully, and an
            // attempt that failed after publishing PaymentAuthorized cannot leave
            // it behind for the saga to act on twice.
            endpoint.UseInMemoryOutbox(context);
            endpoint.UseMessageRetry(retry => retry.Interval(3, TimeSpan.FromMilliseconds(200)));

            endpoint.ConfigureConsumer<AuthorizePaymentConsumer>(context);
            endpoint.ConfigureConsumer<RefundPaymentConsumer>(context);
        });
    });
});

await builder.Build().RunAsync();
