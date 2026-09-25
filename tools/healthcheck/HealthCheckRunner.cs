using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Defra.WasteObligations.HealthCheck;

public sealed class HealthCheckRunner(HttpClient client)
{
    public async Task<HealthCheckResult> Check(
        string serviceName,
        ServiceConfiguration service,
        int timeoutSeconds,
        CancellationToken cancellationToken
    )
    {
        var timer = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        int? httpStatus = null;
        var stage = "Health request";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, service.Url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (service.OAuth is not null)
            {
                stage = "OAuth";
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    await GetToken(service.OAuth, timeout.Token)
                );
            }
            if (service.ApiKey is not null)
                request.Headers.Add(service.ApiKey.HeaderName, service.ApiKey.Value);
            stage = "Health request";
            using var response = await client.SendAsync(request, timeout.Token);
            httpStatus = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode && httpStatus != 503)
                return Result("HTTP failure");

            using var report = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            var status = ReadStatus(report.RootElement);
            var checks = new Dictionary<string, string>();
            ReadChecks(report.RootElement, "", checks);

            return new HealthCheckResult(
                serviceName,
                response.IsSuccessStatusCode && status == "Healthy" && checks.Values.All(x => x == "Healthy"),
                status,
                httpStatus,
                timer.ElapsedMilliseconds,
                checks
            );
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result($"Unhealthy ({stage} timed out after {timeoutSeconds}s)");
        }
        catch (HttpRequestException)
        {
            return Result($"{stage} failed");
        }
        catch (JsonException)
        {
            return Result($"{stage} returned invalid JSON");
        }
        catch (InvalidOperationException)
        {
            return Result($"{stage} returned an invalid response");
        }
        catch (FormatException)
        {
            return Result($"{stage} returned an invalid response");
        }

        HealthCheckResult Result(string status) =>
            new(serviceName, false, status, httpStatus, timer.ElapsedMilliseconds, new Dictionary<string, string>());
    }

    private async Task<string> GetToken(OAuthConfiguration options, CancellationToken cancellationToken)
    {
        var fields = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret,
        };
        using var content = new FormUrlEncodedContent(fields);
        using var response = await client.PostAsync(options.TokenUrl, content, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (
            body.RootElement.ValueKind != JsonValueKind.Object
            || !body.RootElement.TryGetProperty("access_token", out var token)
            || token.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(token.GetString())
            || token.GetString()!.Any(char.IsWhiteSpace)
        )
            throw new InvalidOperationException("Invalid token response.");

        return token.GetString()!;
    }

    private static string ReadStatus(JsonElement element)
    {
        if (
            element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("status", out var status)
            || status.ValueKind != JsonValueKind.String
        )
            return "Unknown";

        return status.GetString() switch
        {
            "Healthy" => "Healthy",
            "Degraded" => "Degraded",
            "Unhealthy" => "Unhealthy",
            _ => "Unknown",
        };
    }

    private static void ReadChecks(JsonElement report, string prefix, Dictionary<string, string> checks)
    {
        if (report.ValueKind != JsonValueKind.Object || !report.TryGetProperty("results", out var results))
            return;
        if (results.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Invalid results.");
        foreach (var entry in results.EnumerateObject())
        {
            var name = prefix + entry.Name;
            checks[name] = ReadStatus(entry.Value);
            if (entry.Value.ValueKind == JsonValueKind.Object && entry.Value.TryGetProperty("response", out var nested))
            {
                if (nested.ValueKind == JsonValueKind.Object && nested.TryGetProperty("status", out _))
                    checks[name + "/response"] = ReadStatus(nested);
                ReadChecks(nested, name + "/", checks);
            }
        }
    }
}
