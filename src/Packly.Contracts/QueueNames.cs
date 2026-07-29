namespace Packly.Contracts;

/// <summary>
/// Names of the receive endpoints each service listens on.
/// </summary>
/// <remarks>
/// Events are published and routed by message type, so publishers never need these.
/// Commands are different: they are sent to one known service, and the sender has to
/// name it. Keeping the names here means a queue is renamed in one place rather than
/// in every service that sends to it.
/// </remarks>
public static class QueueNames
{
    /// <summary>Hosts the order saga.</summary>
    public const string Orchestrator = "packly-orchestrator";

    /// <summary>Handles authorisation and refund commands.</summary>
    public const string Payment = "packly-payment";

    /// <summary>Handles stock reservation and packing commands.</summary>
    public const string Inventory = "packly-inventory";

    /// <summary>Reacts to terminal order statuses.</summary>
    public const string Notification = "packly-notification";

    /// <summary>Builds the read model.</summary>
    public const string Projection = "packly-projection";

    /// <summary>Bridges bus events to connected browsers.</summary>
    public const string ApiNotifications = "packly-api-notifications";
}
