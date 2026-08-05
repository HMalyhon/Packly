using MassTransit;
using Packly.Contracts;

namespace Packly.Orchestrator;

/// <summary>
/// One order's position in the workflow, as persisted between messages.
/// </summary>
/// <remarks>
/// Holds only what a later step needs to decide something. It is not a copy of
/// the order - the write model owns that - and every field here is here because
/// the answer to some message arrives long after the question was asked.
/// </remarks>
public sealed class OrderState : SagaStateMachineInstance
{
    /// <summary>Gets or sets the saga's identity, which is the order id.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the SQL Server rowversion used for optimistic concurrency.
    /// </summary>
    /// <remarks>
    /// Counts database writes, unlike <see cref="StatusVersion"/>, which counts
    /// published transitions. See OrderStateMap for why it is a rowversion.
    /// </remarks>
    public byte[] RowVersion { get; set; } = [];

    /// <summary>Gets or sets the current state name, persisted as text.</summary>
    public string CurrentState { get; set; } = string.Empty;

    /// <summary>Gets or sets the customer the order belongs to.</summary>
    public string CustomerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the order total, needed to authorise payment.</summary>
    public decimal Total { get; set; }

    /// <summary>
    /// Gets or sets the handle for the authorised payment, empty until there is
    /// one. A refund has to name the authorisation it reverses, and by the time
    /// stock fails that happened several messages ago.
    /// </summary>
    public string PaymentReference { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets why the order is being cancelled, empty unless it is. Held
    /// over the refund round trip, because the confirmation does not echo it back.
    /// </summary>
    public string CancellationReason { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the lines the order was placed for. Carried because inventory
    /// needs them and reading them back out of the write model would give the
    /// orchestrator a dependency on another service's database.
    /// </summary>
    public List<OrderLine> Lines { get; set; } = [];

    /// <summary>
    /// Gets or sets how many status changes have been published for this order.
    /// Stamped onto each one so the projection can discard a stale message.
    /// </summary>
    public int StatusVersion { get; set; }
}
