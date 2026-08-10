using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Packly.Api.Features.Orders;
using Packly.Api.Persistence;
using Packly.Messaging;
using Packly.ReadModel;

var builder = WebApplication.CreateBuilder(args);

// The write side. Everything the API records goes through here and nothing reads
// it back: the two connections below are the CQRS split, made literal.
builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("OrdersDb"),
        sql => sql.EnableRetryOnFailure()));

// The read side. Written by the projection service, only queried here.
builder.Services.AddPacklyReadModel(builder.Configuration);

builder.Services.AddSignalR();

builder.Services.AddMassTransit(bus =>
{
    bus.AddConsumer<OrderStatusBroadcaster>();

    // The transactional outbox is the reason this API can promise that an order
    // and its OrderSubmitted event are either both recorded or neither is.
    // Publishing writes a row through the same DbContext, so the message is
    // committed by the same transaction as the order; a delivery service then
    // moves it to RabbitMQ. Publishing straight to the broker instead would leave
    // two ways to be wrong: an order nobody is told about if the broker is down,
    // or an event for an order that was rolled back.
    bus.AddEntityFrameworkOutbox<OrdersDbContext>(outbox =>
    {
        outbox.UseSqlServer();
        outbox.UseBusOutbox();
    });

    bus.UsingRabbitMq((context, rabbit) =>
    {
        rabbit.ConfigurePacklyHost(builder.Configuration);

        // Temporary and named per instance, unlike every other service's queue.
        // A browser watching an order is connected to one API replica, and any
        // replica may hold it, so all of them need every status change. A shared
        // durable queue would make replicas competing consumers and deliver each
        // update to exactly one - the other browsers would simply never hear.
        rabbit.ReceiveEndpoint(
            new TemporaryEndpointDefinition(),
            endpoint => endpoint.ConfigureConsumer<OrderStatusBroadcaster>(context));
    });
});

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Packly", Version = "v1" });

    // Swashbuckle maps decimal to "double" by default. Publishing money as a
    // binary float invites clients to reintroduce exactly the rounding error the
    // endpoint rejects, in a system that validates two decimal places and stores
    // decimal(18,2). "decimal" is not in the OpenAPI spec but generators
    // understand it, and it is honest about intent in a way "double" is not.
    options.MapType<decimal>(() => new OpenApiSchema { Type = JsonSchemaType.Number, Format = "decimal" });
    options.MapType<decimal?>(() => new OpenApiSchema { Type = JsonSchemaType.Number, Format = "decimal" });

    // Every project's documentation, not just this one: the response types and
    // the OrderStatus values they carry are declared in Packly.Contracts and
    // Packly.ReadModel, and reading only Packly.Api.xml left them undescribed.
    foreach (var documentation in Directory.GetFiles(AppContext.BaseDirectory, "Packly.*.xml"))
    {
        options.IncludeXmlComments(documentation);
    }
});

builder.Services.AddProblemDetails();

var app = builder.Build();

// Applied at startup because this stack has to come up with a single command. A
// production deployment would run migrations as their own step rather than have
// every instance race to apply them.
await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
    await dbContext.Database.MigrateAsync();
}

// A malformed body - empty, truncated, or with a field of the wrong type - is
// rejected by model binding before the handler runs, as a BadHttpRequestException
// that already carries a 400. Without this selector the bare exception handler
// reports all of them as 500, so the API would advertise a 400 in its OpenAPI
// document that ordinary client mistakes never actually produce.
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception => exception is BadHttpRequestException badRequest
        ? badRequest.StatusCode
        : StatusCodes.Status500InternalServerError,
});

app.UseStatusCodePages();

// The live order page is served from wwwroot at the root, so opening the
// container's port lands a reviewer on the demo rather than on a schema.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Packly v1");
});

app.MapSubmitOrder();
app.MapGetOrderStatus();
app.MapListOrders();
app.MapHub<OrderHub>(OrderHub.Path);

await app.RunAsync();
