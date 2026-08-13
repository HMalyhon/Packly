using MassTransit.Logging;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Packly.Messaging;

/// <summary>
/// Reports what a service did to whichever collector the environment names.
/// </summary>
/// <remarks>
/// Here rather than in a project of its own because the thing being observed is
/// the bus: MassTransit already opens an activity per send, publish and consume,
/// and carries the trace context in the message headers, so one order crossing
/// four services arrives as one trace rather than four unrelated ones. Subscribing
/// to that is all this does.
/// </remarks>
public static class TelemetryConfiguration
{
    /// <summary>
    /// Exports this service's traces.
    /// </summary>
    /// <param name="services">The container to register into.</param>
    /// <param name="serviceName">How this service names itself in a trace.</param>
    /// <param name="configure">Instrumentation only this service has, if any.</param>
    public static void AddPacklyTelemetry(
        this IServiceCollection services,
        string serviceName,
        Action<TracerProviderBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing.AddSource(DiagnosticHeaders.DefaultListenerName);

                // Reads OTEL_EXPORTER_OTLP_ENDPOINT, so no service names a
                // collector in code and running outside Docker needs no change:
                // with nothing listening the export fails quietly and the service
                // carries on.
                tracing.AddOtlpExporter();

                configure?.Invoke(tracing);
            });
    }
}
