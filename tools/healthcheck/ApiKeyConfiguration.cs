namespace Defra.WasteObligations.HealthCheck;

public sealed class ApiKeyConfiguration
{
    public string HeaderName { get; init; } = "X-Health-Check-Token";
    public string Value { get; init; } = "";

    internal void Validate()
    {
        using var request = new HttpRequestMessage();
        if (
            string.IsNullOrWhiteSpace(HeaderName)
            || HeaderName.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
            || HeaderName.Equals("Host", StringComparison.OrdinalIgnoreCase)
        )
            throw new ArgumentException("API key header must be a custom request header.");
        HealthCheckConfiguration.ValidateCredential(Value);
        request.Headers.Add(HeaderName, Value);
    }
}
