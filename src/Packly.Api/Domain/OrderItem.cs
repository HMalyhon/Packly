namespace Packly.Api.Domain;

/// <summary>
/// A single line of an order.
/// </summary>
/// <remarks>
/// Price and name are copied onto the line rather than referenced from a product
/// table. An order is a record of what was agreed at a point in time, so a later
/// price change must not rewrite history.
/// </remarks>
public sealed class OrderItem
{
    /// <summary>
    /// Decimal places a price may carry, matching the decimal(18,2) column it is
    /// stored in.
    /// </summary>
    public const int PriceScale = 2;

    // EF Core materialises entities through this.
    private OrderItem()
    {
        Sku = string.Empty;
        Name = string.Empty;
    }

    private OrderItem(string sku, string name, int quantity, decimal unitPrice)
    {
        Sku = sku;
        Name = name;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    /// <summary>Gets the stock keeping unit ordered.</summary>
    public string Sku { get; private set; }

    /// <summary>Gets the product name as it was when ordered.</summary>
    public string Name { get; private set; }

    /// <summary>Gets how many units were ordered.</summary>
    public int Quantity { get; private set; }

    /// <summary>Gets the price per unit as it was when ordered.</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Gets what this line contributes to the order total.</summary>
    public decimal LineTotal => Quantity * UnitPrice;

    /// <summary>Creates a line.</summary>
    /// <param name="sku">The stock keeping unit.</param>
    /// <param name="name">The product name at time of ordering.</param>
    /// <param name="quantity">Units ordered; must be positive.</param>
    /// <param name="unitPrice">
    /// Price per unit. May not be negative, and may not carry more than
    /// <see cref="PriceScale"/> decimal places.
    /// </param>
    /// <returns>The new line.</returns>
    public static OrderItem Create(string sku, string name, int quantity, decimal unitPrice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegative(unitPrice);

        // The column is decimal(18,2), and SQL Server rounds silently on the way
        // in. Accepting 1.005 here would mean returning a total computed from
        // 1.005 while the database stores 1.01 - the API and the row would then
        // disagree about what the customer owes.
        if (decimal.Round(unitPrice, PriceScale) != unitPrice)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unitPrice),
                unitPrice,
                $"A unit price may not have more than {PriceScale} decimal places.");
        }

        return new OrderItem(sku, name, quantity, unitPrice);
    }
}
