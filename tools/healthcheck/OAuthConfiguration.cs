namespace Defra.WasteObligations.HealthCheck;

public sealed class OAuthConfiguration
{
    public string TokenUrl { get; init; } = "";
    public string ClientId { get; init; } = "";
    public string ClientSecret { get; init; } = "";

    internal void Validate()
    {
        HealthCheckConfiguration.ValidateAddress(TokenUrl);
        HealthCheckConfiguration.ValidateCredential(ClientId);
        HealthCheckConfiguration.ValidateCredential(ClientSecret);
    }
}
