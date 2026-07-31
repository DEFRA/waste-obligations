using System.Diagnostics.CodeAnalysis;
using Defra.WasteObligations.Api.Utils.Http;
using Defra.WasteObligations.Api.Utils.OAuth2;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Utils.Health;

[ExcludeFromCodeCoverage]
public class OAuth2DownstreamHealthCheck<TOptions>(
    IServiceProvider serviceProvider,
    string name,
    string healthEndpoint,
    Action<TOptions, HttpClient> configureHttpClient
) : IHealthCheck
    where TOptions : OAuth2Options
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        var data = new Dictionary<string, object>();

        try
        {
            _ = await serviceProvider.GetRequiredKeyedService<OAuth2TokenCache>(name).GetToken(cancellationToken);
            data["accessToken"] = new { status = "Retrieved" };
        }
        catch (Exception ex)
        {
            data["accessToken"] = new { status = "Failed" };

            return Failed(context, $"Failed to retrieve an access token for {name}", ex, data);
        }

        var endpoint = healthEndpoint;

        try
        {
            var proxyHandler = serviceProvider.GetRequiredService<ProxyHttpMessageHandler>();
            var oAuth2Handler = serviceProvider.GetRequiredKeyedService<OAuth2Handler>(name);
            oAuth2Handler.InnerHandler = proxyHandler;

            using var httpClient = new HttpClient(oAuth2Handler);
            var options = serviceProvider.GetRequiredService<IOptions<TOptions>>().Value;
            configureHttpClient(options, httpClient);
            endpoint = $"{httpClient.BaseAddress}{healthEndpoint}";

            using var response = await httpClient.GetAsync(healthEndpoint, cancellationToken);

            data["downstream"] = new
            {
                status = response.IsSuccessStatusCode ? "Succeeded" : "Failed",
                endpoint,
                statusCode = (int)response.StatusCode,
            };
            response.EnsureSuccessStatusCode();

            return HealthCheckResult.Healthy($"Connected to {endpoint}", data);
        }
        catch (Exception ex)
        {
            if (!data.ContainsKey("downstream"))
                data["downstream"] = new { status = "Failed", endpoint };

            return Failed(context, $"Failed to connect to {name} after retrieving an access token", ex, data);
        }
    }

    private static HealthCheckResult Failed(
        HealthCheckContext context,
        string message,
        Exception exception,
        IReadOnlyDictionary<string, object> data
    )
    {
        return new HealthCheckResult(
            context.Registration.FailureStatus,
            exception: new Exception(message, exception),
            data: data
        );
    }
}
