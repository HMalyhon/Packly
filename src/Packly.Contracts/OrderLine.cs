namespace Packly.Contracts;

/// <summary>
/// A single line of an order. Carried on messages, so it is pure data:
/// no computed members, because they would be serialised onto the wire
/// and then silently ignored on the way back in.
/// </summary>
public sealed record OrderLine(
    string Sku,
    string Name,
    int Quantity,
    decimal UnitPrice);
