using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Packly.Api.Persistence;

namespace Packly.Api.Health;

/// <summary>
/// Reports whether the side that accepts orders can reach SQL Server.
/// </summary>
internal sealed class WriteModelHealthCheck(OrdersDbContext dbContext) : IHealthCheck
{
    // Bounded for the same reason the connection is opened directly: a probe that
    // takes the provider's default fifteen seconds to fail is not a probe.
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // The raw connection rather than CanConnectAsync, which goes through the
        // retrying execution strategy and retries a failure before reporting it.
        var connection = dbContext.Database.GetDbConnection();

        using var probe = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        probe.CancelAfter(ProbeTimeout);

        try
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(probe.Token);
            }

            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy($"SQL Server did not answer within {ProbeTimeout}.");
        }
        catch (DbException exception)
        {
            return HealthCheckResult.Unhealthy("SQL Server is not answering.", exception);
        }
    }
}
