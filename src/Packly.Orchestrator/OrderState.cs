using MassTransit;
using Packly.Contracts;

namespace Packly.Orchestrator;

/// <summary>
/// One order's position in the workflow, as persisted between messages.
/// </summary>
/// <remarks>
/// A saga instance holds only what later steps need to make decisions. It is not
/// a copy of the order: the write model already owns that, and duplicating it
/// here would create a second thing to keep correct.
/// </remarks>
public sealed class OrderState : SagaStateMachineInstance
{
    /// <summary>
    /// Gets or sets the saga's identity, which is the order id. Correlating on the
    /// order rather than a new key is what lets any service's message about an
    /// order find the right instance.
    /// </summary>
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the SQL Server rowversion used for optimistic concurrency.
    /// </summary>
    /// <remarks>
    /// Maintained by the database, which stamps a new value on every update. That
    /// matters: MassTransit's Entity Framework repository ignores
    /// <c>ISagaVersion</c> - only the document-database repositories honour it -
    /// and relies on Entity Framework's own concurrency token instead. A plain int
    /// that nothing increments would produce a concurrency check that always
    /// passes. Deliberately distinct from <see cref="StatusVersion"/>: this counts
    /// database writes, that counts published transitions.
    /// </remarks>
    public byte[] RowVersion { get; set; } = [];

    /// <summary>Gets or sets the current state name, persisted as text.</summary>
    public string CurrentState { get; set; } = string.Empty;

    /// <summary>Gets or sets the customer the order belongs to.</summary>
    public string CustomerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the order total, needed to authorise payment.</summary>
    public decimal Total { get; set; }

    /// <summary>
    /// Gets or sets the handle for the authorised payment, empty until there is one.
    /// </summary>
    /// <remarks>
    /// Kept because a refund has to name the authorisation it reverses, and by the
    /// time stock fails the authorisation happened several messages ago. This is
    /// the whole reason compensation needs saga state rather than a stateless
    /// reaction to the failure event.
    /// </remarks>
    public string PaymentReference { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets why the order is being cancelled, empty unless it is.
    /// </summary>
    /// <remarks>
    /// Held over the refund round trip. The customer is told nothing until the
    /// money is actually back, and by then the event that explains why has long
    /// been handled; the refund confirmation does not echo it, because the payment
    /// service has no business knowing why it was asked.
    /// </remarks>
    public string CancellationReason { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the lines the order was placed for.
    /// </summary>
    /// <remarks>
    /// Carried because the inventory service needs them and the saga is the only
    /// thing that still knows the order by the time stock is reserved. Reading
    /// them back out of the write model instead would give the orchestrator a
    /// dependency on another service's database, which is the coupling this
    /// architecture is arranged to avoid.
    /// </remarks>
    public List<OrderLine> Lines { get; set; } = [];

    /// <summary>
    /// Gets or sets how many status changes have been published for this order.
    /// </summary>
    /// <remarks>
    /// Stamped onto every OrderStatusChanged so the projection can discard a
    /// redelivered or out-of-order message instead of moving an order backwards.
    /// </remarks>
    public int StatusVersion { get; set; }
}
