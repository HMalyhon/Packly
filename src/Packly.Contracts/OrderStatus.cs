using System.Text.Json.Serialization;

namespace Packly.Contracts;

/// <summary>
/// The publicly visible status of an order.
/// </summary>
/// <remarks>
/// <para>
/// This is the contract the read model and the live status page are built on,
/// and it is deliberately not the same thing as the orchestrator's internal
/// saga states. The saga distinguishes "waiting for payment" from "payment
/// authorised"; a customer only ever needs to see milestones that have been
/// reached. Keeping the two separate means the saga can gain intermediate
/// states without breaking every consumer.
/// </para>
/// <para>
/// Serialised by name everywhere, because the attribute travels with the type
/// rather than with one serializer's configuration. An ordinal on the wire
/// forces every consumer to keep a copy of this list, and silently means
/// something else the day a value is inserted in the middle.
/// </para>
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<OrderStatus>))]
public enum OrderStatus
{
    /// <summary>Order was accepted and durably recorded, nothing else has happened yet.</summary>
    Submitted = 0,

    /// <summary>Funds were successfully authorised against the customer's payment method.</summary>
    PaymentAuthorized = 1,

    /// <summary>All requested lines were reserved from available stock.</summary>
    StockReserved = 2,

    /// <summary>The order is being picked and packed.</summary>
    Packing = 3,

    /// <summary>Terminal: the order was packed and is on its way.</summary>
    Completed = 4,

    /// <summary>Terminal: payment was declined, so the order never proceeded.</summary>
    Rejected = 5,

    /// <summary>Terminal: stock could not be reserved after payment, which was therefore refunded.</summary>
    Cancelled = 6,
}
