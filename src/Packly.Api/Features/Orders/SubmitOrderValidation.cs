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
        else if (request.CustomerId.Length > Order.CustomerIdMaxLength)
        {
            errors[nameof(request.CustomerId)] =
                [$"A customer id may not exceed {Order.CustomerIdMaxLength} characters."];
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            errors[nameof(request.Items)] = ["An order must contain at least one item."];
            return false;
        }

        // Refused here rather than line by line: the answer is about the order, and
        // reporting it once beats ten thousand identical complaints.
        if (request.Items.Count > Order.MaxLines)
        {
            errors[nameof(request.Items)] =
                [$"An order may not contain more than {Order.MaxLines} items."];
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

            // Length is checked here and not only by the column. Without it an
            // over-long field passes validation, reaches SaveChanges, and comes back
            // as a 500 - a client mistake escaping as a server error, which is the
            // one outcome this method's signature says cannot happen.
            if (string.IsNullOrWhiteSpace(item.Sku))
            {
                problems.Add("A sku is required.");
            }
            else if (item.Sku.Length > OrderItem.SkuMaxLength)
            {
                problems.Add($"A sku may not exceed {OrderItem.SkuMaxLength} characters.");
            }

            if (string.IsNullOrWhiteSpace(item.Name))
            {
                problems.Add("A name is required.");
            }
            else if (item.Name.Length > OrderItem.NameMaxLength)
            {
                problems.Add($"A name may not exceed {OrderItem.NameMaxLength} characters.");
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
