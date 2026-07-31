using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Packly.Orchestrator.Persistence;

/// <summary>
/// Maps saga instances to SQL Server.
/// </summary>
internal sealed class OrderStateMap : SagaClassMap<OrderState>
{
    protected override void Configure(
        EntityTypeBuilder<OrderState> entity,
        ModelBuilder model)
    {
        entity.ToTable("OrderState");

        // Persisted as text so the table is readable: the row says
        // "AwaitingPayment" rather than an ordinal nobody can decode without the
        // source. Long enough for the longest state name.
        entity.Property(state => state.CurrentState).HasMaxLength(64);

        entity.Property(state => state.CustomerId).HasMaxLength(128);
        entity.Property(state => state.Total).HasPrecision(18, 2);

        // SQL Server stamps a new value on every update, so a stale write fails
        // with DbUpdateConcurrencyException instead of quietly overwriting the
        // winner. It has to be the database doing this: MassTransit's Entity
        // Framework repository ignores ISagaVersion, and a plain counter nothing
        // increments would make the concurrency check always pass.
        entity.Property(state => state.RowVersion).IsRowVersion();
    }
}
