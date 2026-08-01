namespace Packly.Api.Domain;

/// <summary>
/// An order as the write side understands it: the record of what a customer asked
/// for.
/// </summary>
/// <remarks>
/// <para>
/// This is the write model, and it is shaped for correctness rather than for
/// reading. Queries will be served from the projection in MongoDB instead, which
/// is what lets this type stay normalised and free of display concerns.
/// </para>
/// <para>
/// Deliberately carries no status. How far an order has travelled through the
/// workflow is the orchestrator's business, and a copy here would be a second
/// source of truth that nothing updates - permanently stuck at "Submitted" while
/// the real answer moved on.
/// </para>
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
    /// <exception cref="ArgumentException">
    /// Thrown when no lines are supplied. The endpoint rejects this case first and
    /// returns a validation problem; the guard exists so the invariant holds even
    /// if the aggregate is ever constructed from somewhere else.
    /// </exception>
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
