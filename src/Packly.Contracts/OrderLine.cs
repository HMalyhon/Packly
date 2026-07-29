namespace Packly.Contracts;

/// <summary>
/// A single line of an order. Carried on messages, so it is pure data:
/// no computed members, because they would be serialised onto the wire
/// and then silently ignored on the way back in.
/// </summary>
/// <param name="Sku">Stock keeping unit identifying the product.</param>
/// <param name="Name">Display name captured at the time of ordering.</param>
/// <param name="Quantity">Number of units ordered.</param>
/// <param name="UnitPrice">Price per unit at the time of ordering.</param>
public sealed record OrderLine(
    string Sku,
    string Name,
    int Quantity,
    decimal UnitPrice);
