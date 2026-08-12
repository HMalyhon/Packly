using Packly.Contracts;

namespace Packly.Api.Features.Orders;

/// <summary>
/// The query parameters of a list request, checked before the database sees them.
/// </summary>
internal static class ListOrdersQuery
{
    internal const int DefaultPageSize = 20;

    // A cap rather than a suggestion: without one a single request can ask the
    // database for the whole collection and the API will serialise all of it.
    internal const int MaxPageSize = 100;

    /// <summary>
    /// Checks the parameters and works out how far into the collection to skip.
    /// </summary>
    internal static bool TryValidate(
        OrderStatus? status,
        int page,
        int pageSize,
        out int skip,
        out Dictionary<string, string[]> errors)
    {
        errors = [];
        skip = 0;

        if (pageSize is < 1 or > MaxPageSize)
        {
            errors[nameof(pageSize)] = [$"Page size must be between 1 and {MaxPageSize}."];
        }

        // Widened before multiplying, because the product of two valid ints is not
        // necessarily one. page=21474838 with a page size of 100 wrapped to a
        // negative skip, which the driver rejected as an unhandled 500 - a bad
        // request escaping as a server error the handler's signature says cannot
        // happen.
        var offset = ((long)page - 1) * pageSize;

        if (page < 1 || offset > int.MaxValue)
        {
            errors[nameof(page)] = ["Page must be 1 or greater, and within range for the page size."];
        }

        // An undefined value casts cleanly from its number, so ?status=99 binds
        // without complaint and then matches nothing. Reported as the bad request
        // it is rather than answered with an empty page that implies there are no
        // such orders.
        if (status is not null && !Enum.IsDefined(status.Value))
        {
            errors[nameof(status)] = ["Unknown order status."];
        }

        if (errors.Count > 0)
        {
            return false;
        }

        skip = (int)offset;
        return true;
    }
}
