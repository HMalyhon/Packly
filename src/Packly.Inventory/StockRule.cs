using Packly.Contracts;

namespace Packly.Inventory;

/// <summary>
/// What the warehouse simulation decides, apart from how it is delivered.
/// </summary>
internal static class StockRule
{
    // In the sku, so nothing has to be seeded and the same order always answers the
    // same way - which is what makes a redelivered command a duplicate.
    internal const string SoldOutPrefix = "SOLD-OUT";

    /// <summary>Returns the first line that cannot be reserved, or null when all can.</summary>
    internal static OrderLine? FirstUnavailable(IReadOnlyList<OrderLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        return lines.FirstOrDefault(
            line => line.Sku.StartsWith(SoldOutPrefix, StringComparison.OrdinalIgnoreCase));
    }
}
