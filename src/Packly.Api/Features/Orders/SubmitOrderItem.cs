namespace Packly.Api.Features.Orders;

/// <summary>
/// One line of a submission request.
/// </summary>
/// <remarks>
/// Every member is nullable because this is untrusted input: a caller can omit
/// anything, and the endpoint is responsible for saying so clearly rather than
/// letting the model binder produce a silent default.
/// </remarks>
/// <param name="Sku">The stock keeping unit being ordered.</param>
/// <param name="Name">Display name of the product.</param>
/// <param name="Quantity">Units wanted; must be greater than zero.</param>
/// <param name="UnitPrice">Price per unit; may not be negative.</param>
public sealed record SubmitOrderItem(
    string? Sku,
    string? Name,
    int? Quantity,
    decimal? UnitPrice);
