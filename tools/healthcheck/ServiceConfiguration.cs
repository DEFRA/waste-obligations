namespace Defra.WasteObligations.HealthCheck;

public sealed class ServiceConfiguration
{
    public string Url { get; init; } = "";
    public ApiKeyConfiguration? ApiKey { get; init; }
    public OAuthConfiguration? OAuth { get; init; }
}
