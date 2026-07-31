using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;

namespace Packly.Orchestrator.Persistence;

/// <summary>
/// Stores saga state and the orchestrator's outbox.
/// </summary>
/// <remarks>
/// Saga state lives in a database rather than in memory so that an order in
/// flight survives this service being restarted, redeployed or scaled out. That
/// durability is the difference between a workflow and a sequence of hopeful
/// method calls.
/// </remarks>
/// <param name="options">Provider and connection configuration.</param>
public sealed class OrderStateDbContext(DbContextOptions<OrderStateDbContext> options)
    : SagaDbContext(options)
{
    /// <inheritdoc />
    protected override IEnumerable<ISagaClassMap> Configurations
    {
        get { yield return new OrderStateMap(); }
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // A transition and the events announcing it commit together, for the same
        // reason the API's order and its OrderSubmitted do: otherwise a crash
        // between the two leaves the read model describing an order that has since
        // moved on, with nothing to correct it.
        modelBuilder.AddTransactionalOutboxEntities();

        base.OnModelCreating(modelBuilder);
    }
}
