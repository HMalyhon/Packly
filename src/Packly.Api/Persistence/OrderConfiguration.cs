using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Packly.Api.Domain;

namespace Packly.Api.Persistence;

/// <summary>
/// Maps the order aggregate to SQL Server.
/// </summary>
/// <remarks>
/// Only what convention gets wrong is configured here. Non-nullable CLR
/// properties already become NOT NULL columns, and derived properties with no
/// backing field are never mapped, so declaring either would be noise.
/// </remarks>
internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(order => order.Id);

        builder.Property(order => order.CustomerId).HasMaxLength(128);

        // Stored as text rather than an int. The extra bytes cost nothing at this
        // scale and it means anyone reading the table sees "Submitted" rather than
        // having to map 0 back to a name.
        builder.Property(order => order.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        // Lines are owned: they have no meaning outside their order, cannot be
        // queried independently, and are deleted with it. That is the aggregate
        // boundary expressed in the mapping rather than only in documentation.
        // EF gives them a composite key of the owner plus a shadow identity, so
        // no surrogate key has to be invented on the entity itself.
        builder.OwnsMany(order => order.Items, items =>
        {
            items.ToTable("OrderItems");
            items.WithOwner().HasForeignKey("OrderId");

            items.Property(item => item.Sku).HasMaxLength(64);
            items.Property(item => item.Name).HasMaxLength(256);

            // Matches OrderItem.PriceScale, which submission validates against.
            // Stated explicitly because money is the one place a silently changed
            // provider default would be expensive.
            items.Property(item => item.UnitPrice).HasPrecision(18, 2);
        });
    }
}
