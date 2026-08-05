using MassTransit;
using Microsoft.Extensions.Configuration;

namespace Packly.Messaging;

/// <summary>
/// Connects a service to the broker.
/// </summary>
/// <remarks>
/// Only the connection is shared. Each service still declares its own consumers,
/// sagas and endpoints, because hiding those behind a helper would make the
/// topology harder to read rather than easier.
/// </remarks>
public static class RabbitMqConfiguration
{
    private const string SectionName = "RabbitMq";

    /// <summary>
    /// Points the bus at the broker described by configuration.
    /// </summary>
    /// <param name="rabbit">The bus factory being configured.</param>
    /// <param name="configuration">Configuration containing a RabbitMq section.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a required setting is missing. Failing at startup with the name
    /// of the missing key beats connecting to the wrong place, or failing later
    /// with a null reference that says nothing about which service is
    /// misconfigured.
    /// </exception>
    public static void ConfigurePacklyHost(
        this IRabbitMqBusFactoryConfigurator rabbit,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(rabbit);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(SectionName);

        // Optional, so it gets a default rather than the Required treatment - but
        // blank still counts as absent, for the same reason it does there. An empty
        // string is a legal virtual host name that no broker has, and asking for one
        // fails at connect time with "vhost not found" long after startup.
        var virtualHost = section["VirtualHost"];

        rabbit.Host(
            Required(section, "Host"),
            string.IsNullOrWhiteSpace(virtualHost) ? "/" : virtualHost,
            host =>
            {
                host.Username(Required(section, "Username"));
                host.Password(Required(section, "Password"));
            });
    }

    /// <summary>
    /// Reads a setting that the service cannot start without.
    /// </summary>
    /// <remarks>
    /// Blank counts as missing. An empty environment variable is a far more likely
    /// mistake than an absent one - it is what an unset shell variable or an
    /// unresolved compose interpolation expands to - and connecting with an empty
    /// username fails somewhere much further from the cause.
    /// </remarks>
    private static string Required(IConfigurationSection section, string key)
    {
        var value = section[key];

        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
                $"Configuration '{section.Path}:{key}' is required but was not supplied.")
            : value;
    }
}
