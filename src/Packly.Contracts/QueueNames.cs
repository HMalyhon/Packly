namespace Packly.Contracts;

/// <summary>
/// Names of the queues commands are sent to.
/// </summary>
/// <remarks>
/// Events are published and routed by message type, so a publisher never names a
/// destination and no event consumer appears here - each of those names its own
/// endpoint and nothing else needs to know it. Commands are different: they are
/// sent to one known service, and the sender has to say which.
/// </remarks>
public static class QueueNames
{
    /// <summary>Handles authorisation and refund commands.</summary>
    public const string Payment = "packly-payment";

    /// <summary>Handles stock reservation and packing commands.</summary>
    public const string Inventory = "packly-inventory";
}
