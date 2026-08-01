using MassTransit;
using Microsoft.Extensions.Configuration;

namespace Packly.Messaging;

/// <summary>
/// Connects a service to the broker.
/// </summary>
/// <remarks>
/// Extracted once three services needed the same block, not in anticipation of
/// them. What is shared is only the connection: each service still declares its
/// own consumers, sagas and endpoints, because those are the parts that differ
/// and hiding them behind a helper would make the topology harder to read rather
/// than easier.
/// </remarks>
public static class RabbitMqConfiguration
{
    /// <summary>
    /// The configuration section holding broker settings.
    /// </summary>
    public const string SectionName = "RabbitMq";

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

        rabbit.Host(
            Required(section, "Host"),
            section["VirtualHost"] ?? "/",
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
