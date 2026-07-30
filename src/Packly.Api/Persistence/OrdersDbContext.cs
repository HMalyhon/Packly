using Microsoft.EntityFrameworkCore;
using Packly.Api.Domain;

namespace Packly.Api.Persistence;

/// <summary>
/// The write-side database: orders as they are recorded, not as they are queried.
/// </summary>
/// <remarks>
/// Nothing reads from here to answer a user's question. Queries are served from
/// the MongoDB projection, which is the whole point of separating the two models.
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

        base.OnModelCreating(modelBuilder);
    }
}
