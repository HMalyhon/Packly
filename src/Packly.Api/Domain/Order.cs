namespace Packly.Api.Domain;

/// <summary>
/// An order as the write side understands it: the record of what a customer asked
/// for.
/// </summary>
/// <remarks>
/// Deliberately carries no status. How far an order has travelled is the
/// orchestrator's business, and a copy here would be a second source of truth
/// that nothing updates - stuck at "Submitted" while the real answer moved on.
/// </remarks>
public sealed class Order
{
    private readonly List<OrderItem> _items = [];

    // EF Core materialises entities through this; application code uses Submit.
    private Order()
    {
        CustomerId = string.Empty;
    }

    private Order(Guid id, string customerId, DateTimeOffset submittedAt)
    {
        Id = id;
        CustomerId = customerId;
        SubmittedAt = submittedAt;
    }

    /// <summary>Gets the order's identifier, assigned on submission.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the customer who placed the order.</summary>
    public string CustomerId { get; private set; }

    /// <summary>Gets when the order was accepted.</summary>
    public DateTimeOffset SubmittedAt { get; private set; }

    /// <summary>Gets the lines that make up this order.</summary>
    public IReadOnlyList<OrderItem> Items => _items;

    /// <summary>
    /// Gets the order total, summed from its lines rather than stored, so the two
    /// can never disagree.
    /// </summary>
    public decimal Total => _items.Sum(item => item.LineTotal);

    /// <summary>
    /// Records a new order.
    /// </summary>
    /// <param name="customerId">The customer placing the order.</param>
    /// <param name="lines">The requested lines; at least one is required.</param>
    /// <param name="submittedAt">When the order was accepted.</param>
    /// <returns>The new order.</returns>
    /// <exception cref="ArgumentException">No lines were supplied.</exception>
    public static Order Submit(
        string customerId,
        IEnumerable<OrderItem> lines,
        DateTimeOffset submittedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        ArgumentNullException.ThrowIfNull(lines);

        var items = lines as IReadOnlyCollection<OrderItem> ?? [.. lines];

        if (items.Count == 0)
        {
            throw new ArgumentException("An order must contain at least one line.", nameof(lines));
        }

        var order = new Order(Guid.CreateVersion7(), customerId, submittedAt);
        order._items.AddRange(items);

        return order;
    }
}
