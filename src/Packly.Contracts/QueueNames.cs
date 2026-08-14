namespace Packly.Contracts;

/// <summary>
/// Names of the queues commands are sent to.
/// </summary>
/// <remarks>
/// Commands only. Events route by message type, so a publisher never names a
/// destination and no event consumer belongs here.
/// </remarks>
public static class QueueNames
{
    /// <summary>Handles authorisation and refund commands.</summary>
    public const string Payment = "packly-payment";

    /// <summary>Handles stock reservation and packing commands.</summary>
    public const string Inventory = "packly-inventory";
}
