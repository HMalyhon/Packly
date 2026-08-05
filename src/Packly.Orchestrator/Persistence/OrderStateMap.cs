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
        entity.Property(state => state.PaymentReference).HasMaxLength(64);
        entity.Property(state => state.CancellationReason).HasMaxLength(256);

        // Stored as a JSON column rather than a child table. The saga never
        // queries or joins on lines - it carries them from one command to the
        // next - so a second table would add a migration, an owned entity and a
        // join to support a query nobody makes.
        entity.OwnsMany(state => state.Lines, lines =>
        {
            lines.ToJson();

            // Explicit even though JSON stores the value as text. EF warns without
            // it, and money that can be silently truncated is exactly the defect
            // the API validates against on the way in.
            lines.Property(line => line.UnitPrice).HasPrecision(18, 2);
        });

        // It has to be the database stamping this. MassTransit's Entity Framework
        // repository ignores ISagaVersion - only the document-database ones honour
        // it - so a plain counter nothing increments would make every concurrency
        // check pass.
        entity.Property(state => state.RowVersion).IsRowVersion();
    }
}
