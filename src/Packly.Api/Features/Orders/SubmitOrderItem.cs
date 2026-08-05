namespace Packly.Api.Features.Orders;

/// <summary>
/// One line of a submission request. Every member is nullable because this is
/// untrusted input, and the endpoint reports what is missing rather than letting
/// the binder supply a silent default.
/// </summary>
/// <param name="Sku">The stock keeping unit being ordered.</param>
/// <param name="Name">Display name of the product.</param>
/// <param name="Quantity">Units wanted; must be greater than zero.</param>
/// <param name="UnitPrice">Price per unit; may not be negative.</param>
public sealed record SubmitOrderItem(
    string? Sku,
    string? Name,
    int? Quantity,
    decimal? UnitPrice);
