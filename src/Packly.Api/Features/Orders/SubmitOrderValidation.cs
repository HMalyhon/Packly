using Packly.Api.Domain;

namespace Packly.Api.Features.Orders;

/// <summary>
/// Turns a submitted request into an order, or into the reasons it is not one.
/// </summary>
internal static class SubmitOrderValidation
{
    /// <summary>
    /// Collects every problem rather than stopping at the first, so the caller can
    /// fix them in one pass.
    /// </summary>
    internal static bool TryBuildOrder(
        SubmitOrderRequest request,
        TimeProvider timeProvider,
        out Order order,
        out Dictionary<string, string[]> errors)
    {
        errors = [];
        order = null!;

        if (string.IsNullOrWhiteSpace(request.CustomerId))
        {
            errors[nameof(request.CustomerId)] = ["A customer id is required."];
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            errors[nameof(request.Items)] = ["An order must contain at least one item."];
            return false;
        }

        var items = new List<OrderItem>(request.Items.Count);

        for (var index = 0; index < request.Items.Count; index++)
        {
            var item = request.Items[index];
            var field = $"{nameof(request.Items)}[{index}]";

            // JSON arrays can contain nulls, and the deserialiser hands them
            // straight through. Reported like any other bad line rather than
            // dereferenced.
            if (item is null)
            {
                errors[field] = ["An item may not be null."];
                continue;
            }

            var problems = new List<string>();

            if (string.IsNullOrWhiteSpace(item.Sku))
            {
                problems.Add("A sku is required.");
            }

            if (string.IsNullOrWhiteSpace(item.Name))
            {
                problems.Add("A name is required.");
            }

            if (item.Quantity is null or <= 0)
            {
                problems.Add("Quantity must be greater than zero.");
            }

            if (item.UnitPrice is null or < 0)
            {
                problems.Add("Unit price may not be negative.");
            }
            else if (decimal.Round(item.UnitPrice.Value, OrderItem.PriceScale) != item.UnitPrice.Value)
            {
                // Rejected rather than rounded. Silently accepting 1.005 would mean
                // answering with a total the stored order does not add up to.
                problems.Add($"Unit price may not have more than {OrderItem.PriceScale} decimal places.");
            }

            if (problems.Count > 0)
            {
                errors[field] = [.. problems];
                continue;
            }

            items.Add(OrderItem.Create(item.Sku!, item.Name!, item.Quantity!.Value, item.UnitPrice!.Value));
        }

        if (errors.Count > 0)
        {
            return false;
        }

        order = Order.Submit(request.CustomerId!, items, timeProvider.GetUtcNow());
        return true;
    }
}
