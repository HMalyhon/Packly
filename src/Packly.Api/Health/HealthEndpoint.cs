using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Packly.Api.Health;

/// <summary>
/// Reports whether the API can reach both sides of the split.
/// </summary>
public static class HealthEndpoint
{
    /// <summary>
    /// Maps the health endpoint.
    /// </summary>
    /// <param name="app">The route builder to map onto.</param>
    public static void MapHealth(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // A handler rather than MapHealthChecks: that builds an endpoint with no
        // method behind it, which ApiExplorer skips and Swagger therefore cannot
        // document - and this is the one endpoint a reviewer looks for first.
        app.MapGet("/health", HandleAsync)
            .WithName("Health")
            .WithSummary("Report whether the API and everything behind it is answering.")
            .WithDescription(
                "Both sides of the split, plus the broker - MassTransit registers a check of " +
                "its own. Any of them can be down while the rest keep answering, so the " +
                "response names each one rather than giving a single verdict.")
            .WithTags("Health")
            .Produces<HealthResponse>(StatusCodes.Status200OK)
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<Results<Ok<HealthResponse>, JsonHttpResult<HealthResponse>>> HandleAsync(
        HealthCheckService healthChecks,
        CancellationToken cancellationToken)
    {
        var report = await healthChecks.CheckHealthAsync(cancellationToken);

        var response = new HealthResponse(
            report.Status.ToString(),
            report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString()));

        return report.Status == HealthStatus.Unhealthy
            ? TypedResults.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable)
            : TypedResults.Ok(response);
    }
}
