using MassTransit;
using Microsoft.EntityFrameworkCore;
using Packly.Api.Domain;

namespace Packly.Api.Persistence;

/// <summary>
/// The write-side database: orders as they are recorded, not as they are queried.
/// </summary>
/// <remarks>
/// <para>
/// Nothing reads from here to answer a user's question. Queries will be served
/// from the MongoDB projection, which is the whole point of separating the two
/// models.
/// </para>
/// <para>
/// This context also owns the MassTransit outbox tables, and that is deliberate:
/// an outbox only works if the message is written in the same transaction as the
/// data that justifies it.
/// </para>
/// </remarks>
/// <param name="options">Provider and connection configuration.</param>
public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options)
    : DbContext(options)
{
    /// <summary>Gets the orders recorded by the write side.</summary>
    public DbSet<Order> Orders => Set<Order>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly);

        // Adds InboxState, OutboxMessage and OutboxState. Messages are staged in
        // these tables by the same SaveChanges that writes the order, and a
        // delivery service hands them to RabbitMQ afterwards.
        modelBuilder.AddTransactionalOutboxEntities();

        base.OnModelCreating(modelBuilder);
    }
}
