using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Packly.ReadModel;

/// <summary>
/// Connects a service to the read model.
/// </summary>
/// <remarks>
/// The serialisation setup lives here with the documents rather than in each
/// service, because it is part of the same contract. A reader that registered
/// different conventions from the writer fails on the first document it reads,
/// with a message about a field name rather than about the conventions that
/// produced it.
/// </remarks>
public static class ReadModelConfiguration
{
    private const string ConnectionStringName = "ReadModel";
    private const string CollectionName = "order_status";

    /// <summary>
    /// Registers the read model collection and the conventions it is stored with.
    /// </summary>
    /// <param name="services">The container to register into.</param>
    /// <param name="configuration">Configuration carrying the connection string.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection string is missing, blank, or names no database.
    /// Failing at startup naming the setting beats failing on the first query with
    /// an error that says nothing about which value was wrong.
    /// </exception>
    public static void AddPacklyReadModel(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is required but was not supplied.");
        }

        var url = new MongoUrl(connectionString);

        // Checked here rather than left to the driver. A connection string without
        // a database path is an ordinary shape - mongodb://host:27017/?authSource=admin
        // parses perfectly - and the resulting null surfaces later as
        // ArgumentNullException on 'databaseName', which names neither this setting
        // nor the service that was misconfigured.
        if (string.IsNullOrWhiteSpace(url.DatabaseName))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' must name a database, "
                + "for example mongodb://host:27017/PacklyRead.");
        }

        RegisterConventions();

        var collection = new MongoClient(url)
            .GetDatabase(url.DatabaseName)
            .GetCollection<OrderStatusDocument>(CollectionName);

        services.AddSingleton(collection);
    }

    /// <summary>
    /// Applies the process-wide serialisation settings the documents assume.
    /// </summary>
    private static void RegisterConventions()
    {
        // Guids as the standard BSON binary subtype rather than the driver's
        // legacy layout. Without this the serializer refuses to write one at all,
        // because the representation is unspecified by default and it will not
        // guess.
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

        // Field names as camelCase, so a document read in mongosh looks like the
        // JSON the API serves rather than like the C# class behind it.
        ConventionRegistry.Register(
            "camelCase",
            new ConventionPack { new CamelCaseElementNameConvention() },
            _ => true);
    }
}
