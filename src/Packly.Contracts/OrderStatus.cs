using System.Text.Json.Serialization;

namespace Packly.Contracts;

/// <summary>
/// The publicly visible status of an order.
/// </summary>
/// <remarks>
/// Milestones a customer sees, not the orchestrator's internal saga states. The
/// saga distinguishes "waiting for payment" from "payment authorised"; keeping
/// the two apart lets it gain intermediate states without breaking a consumer.
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
