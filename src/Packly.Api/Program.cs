using System.Text.Json.Serialization;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Packly.Api.Features.Orders;
using Packly.Api.Persistence;
using Packly.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("OrdersDb"),
        sql => sql.EnableRetryOnFailure()));

builder.Services.AddMassTransit(bus =>
{
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

        rabbit.ConfigureEndpoints(context);
    });
});

// Injected rather than reading DateTimeOffset.UtcNow directly, so time is a
// dependency a test can control instead of a fact of the environment.
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

    // Surfaces the XML documentation from the source in the generated document,
    // which is the reason GenerateDocumentationFile is on for every project.
    var documentation = Path.Combine(AppContext.BaseDirectory, "Packly.Api.xml");
    if (File.Exists(documentation))
    {
        options.IncludeXmlComments(documentation);
    }
});

// Statuses go out as "Submitted" rather than 0. An integer on the wire forces
// every client to keep its own copy of the mapping, and silently means something
// different the day a value is inserted into the enum.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// The same converter again, because these are two different options objects and
// Swashbuckle only reads this one. Configuring just the line above produced a
// published schema declaring `"type": "integer"` for a field the API actually
// returns as "Submitted" - a contract that contradicted the responses it
// described, served at the root of the container for anyone to generate a broken
// client from.
builder.Services.Configure<JsonOptions>(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

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

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Packly v1");

    // Served at the root: opening the container's port lands a reviewer straight
    // on something they can use.
    options.RoutePrefix = string.Empty;
});

app.MapSubmitOrder();

await app.RunAsync();
