namespace Packly.Api.Health;

/// <summary>
/// Whether the API and everything behind it is answering.
/// </summary>
/// <param name="Status">Healthy only when every check is.</param>
/// <param name="Checks">Each check's own result, so a failure names what is down.</param>
public sealed record HealthResponse(
    string Status,
    IReadOnlyDictionary<string, string> Checks);
