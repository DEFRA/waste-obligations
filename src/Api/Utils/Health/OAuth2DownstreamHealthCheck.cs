using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
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
        TOptions options;

        try
        {
            options = serviceProvider.GetRequiredService<IOptions<TOptions>>().Value;
            var accessToken = await serviceProvider
                .GetRequiredKeyedService<OAuth2TokenCache>(name)
                .GetToken(cancellationToken);
            data["accessToken"] = GetAccessTokenData(accessToken, options.Scope);
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

    private static object GetAccessTokenData(string accessToken, string? scope)
    {
        var claimsAvailable = TryReadAudiences(accessToken, out var audiences);
        var audienceMatchesRequestedScope =
            claimsAvailable && !string.IsNullOrWhiteSpace(scope) ? (bool?)AudiencesMatchScope(audiences, scope) : null;

        return new
        {
            status = "Retrieved",
            requestedScope = scope,
            claimsAvailable,
            audiences,
            audienceMatchesRequestedScope,
        };
    }

    private static bool TryReadAudiences(string accessToken, out string[] audiences)
    {
        audiences = [];
        var sections = accessToken.Split('.');

        if (sections.Length != 3)
            return false;

        try
        {
            var encodedPayload = sections[1].Replace('-', '+').Replace('_', '/');
            var paddingLength = (4 - encodedPayload.Length % 4) % 4;
            var payload = Convert.FromBase64String(encodedPayload.PadRight(encodedPayload.Length + paddingLength, '='));
            using var document = JsonDocument.Parse(payload);

            if (!document.RootElement.TryGetProperty("aud", out var audience))
                return true;

            audiences = audience.ValueKind switch
            {
                JsonValueKind.String => [audience.GetString()!],
                JsonValueKind.Array =>
                [
                    .. audience
                        .EnumerateArray()
                        .Where(x => x.ValueKind == JsonValueKind.String)
                        .Select(x => x.GetString()!),
                ],
                _ => [],
            };

            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool AudiencesMatchScope(IEnumerable<string> audiences, string scope)
    {
        var requestedAudiences = scope
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ScopeToAudience)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return audiences.Any(requestedAudiences.Contains);
    }

    private static string ScopeToAudience(string scope)
    {
        var separatorIndex = scope.IndexOf('/');

        return separatorIndex == -1 ? scope : scope[..separatorIndex];
    }
}
