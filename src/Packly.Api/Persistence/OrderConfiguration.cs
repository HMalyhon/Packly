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

        // Taken from the constant the endpoint validates against, so the column and
        // the boundary cannot drift into disagreeing about what fits.
        builder.Property(order => order.CustomerId).HasMaxLength(Order.CustomerIdMaxLength);

        // Lines are owned: they have no meaning outside their order, cannot be
        // queried independently, and are deleted with it. That is the aggregate
        // boundary expressed in the mapping rather than only in documentation.
        // EF gives them a composite key of the owner plus a shadow identity, so
        // no surrogate key has to be invented on the entity itself.
        builder.OwnsMany(order => order.Items, items =>
        {
            items.ToTable("OrderItems");
            items.WithOwner().HasForeignKey("OrderId");

            items.Property(item => item.Sku).HasMaxLength(OrderItem.SkuMaxLength);
            items.Property(item => item.Name).HasMaxLength(OrderItem.NameMaxLength);

            // Matches OrderItem.PriceScale, which submission validates against.
            // Stated explicitly because money is the one place a silently changed
            // provider default would be expensive.
            items.Property(item => item.UnitPrice).HasPrecision(18, 2);
        });
    }
}
