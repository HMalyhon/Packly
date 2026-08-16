using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;
using Packly.ReadModel;

namespace Packly.Api.Health;

/// <summary>
/// Reports whether the side that answers queries can reach MongoDB.
/// </summary>
internal sealed class ReadModelHealthCheck(IMongoCollection<OrderStatusDocument> collection)
    : IHealthCheck
{
    // The driver spends thirty seconds selecting a server before it gives up, which
    // is long enough that the probe looks hung rather than negative.
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var probe = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        probe.CancelAfter(ProbeTimeout);

        try
        {
            await collection.Database.RunCommandAsync<BsonDocument>(
                new BsonDocument("ping", 1),
                cancellationToken: probe.Token);

            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy($"MongoDB did not answer within {ProbeTimeout}.");
        }
        catch (MongoException exception)
        {
            return HealthCheckResult.Unhealthy("MongoDB is not answering.", exception);
        }
    }
}
