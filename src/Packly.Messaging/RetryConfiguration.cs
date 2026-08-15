using MassTransit;

namespace Packly.Messaging;

/// <summary>
/// The budget every Packly endpoint retries a failed message under.
/// </summary>
/// <remarks>
/// Shared because it drifted. Each service carried its own copy of the same
/// literal, and when the orchestrator's proved too short to survive a database
/// restart it was widened there and nowhere else - leaving the projection, the one
/// writer of a read model nothing replays, still giving up after a second. A policy
/// that lives in one place cannot fall out of step with itself.
/// </remarks>
public static class RetryConfiguration
{
    /// <summary>
    /// Applies the shared retry policy to an endpoint.
    /// </summary>
    /// <remarks>
    /// The first retry is near immediate, which is all an optimistic concurrency
    /// loser needs - MassTransit does not retry on its own, so without it a losing
    /// write dead-letters the message. Each subsequent wait adds two seconds,
    /// reaching about ninety in total, which is what a restart needs: five attempts
    /// at 200ms gave up after a second, and a message arriving during one was
    /// dead-lettered, stranding an otherwise healthy order.
    /// <para>
    /// Configure this before an outbox, never after. MassTransit builds the pipe in
    /// configuration order, so an outbox wrapped around the retry spans every
    /// attempt and flushes the publishes of the one that failed alongside the one
    /// that succeeded - the opposite of what the outbox is there for.
    /// </para>
    /// </remarks>
    /// <param name="endpoint">The endpoint being configured.</param>
    public static void UsePacklyRetry(this IConsumePipeConfigurator endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        endpoint.UseMessageRetry(retry => retry.Incremental(
            retryLimit: 10,
            initialInterval: TimeSpan.FromMilliseconds(200),
            intervalIncrement: TimeSpan.FromSeconds(2)));
    }
}
