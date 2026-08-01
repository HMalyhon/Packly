using MassTransit;
using Packly.Contracts;
using Packly.Payment;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddMassTransit(bus =>
{
    bus.AddConsumer<AuthorizePaymentConsumer>();

    bus.UsingRabbitMq((context, rabbit) =>
    {
        rabbit.Host(
            builder.Configuration["RabbitMq:Host"],
            builder.Configuration["RabbitMq:VirtualHost"] ?? "/",
            host =>
            {
                host.Username(builder.Configuration["RabbitMq:Username"]!);
                host.Password(builder.Configuration["RabbitMq:Password"]!);
            });

        // Named explicitly rather than derived from the consumer type, because the
        // orchestrator addresses this queue by name when it sends a command. The
        // constant is shared so a rename cannot silently break the send.
        rabbit.ReceiveEndpoint(QueueNames.Payment, endpoint =>
        {
            endpoint.UseMessageRetry(retry => retry.Interval(3, TimeSpan.FromMilliseconds(200)));

            endpoint.ConfigureConsumer<AuthorizePaymentConsumer>(context);
        });
    });
});

await builder.Build().RunAsync();
