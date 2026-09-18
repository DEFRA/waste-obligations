# System health CLI

.NET 10 CLI for checking multiple services and their dependencies. Run from the repository root:

```sh
cp tools/healthcheck/.env.example tools/healthcheck/.env
# Edit appsettings.json with service URLs; fill in credentials in .env.
dotnet run --project tools/healthcheck
# Machine-readable results:
dotnet run --project tools/healthcheck -- --json
```

`appsettings.json` uses placeholder domains and tenant; replace them with the actual environment's addresses. Credentials belong in the adjacent `.env` file, which is ignored by Git and excluded from build/publish output and the Docker context. `.env.example` contains variable names only. Existing process environment variables take precedence over `.env`.

By default the CLI finds `tools/healthcheck/appsettings.json` when run from the repository root, then `appsettings.json` in the current directory, then beside the executable. An explicit configuration path can also be supplied: `dotnet run --project tools/healthcheck -- /path/to/appsettings.json`. The `.env` file is always loaded from the selected configuration file’s directory. Any number of services can be configured; each is keyed by its unique service name under `Services` and needs an absolute `Url`, including the health path and any query string. URLs must use HTTPS, except loopback HTTP for local checks.

## Endpoints

Verified against the local service repositories:

| Service | Path | Endpoint authentication |
| --- | --- | --- |
| epr-packaging-frontend | `/admin/health/all` | `X-Health-Check-Token` (configurable in the service) |
| waste-obligations | `/health/all` | Anonymous; the gateway may require OAuth |
| waste-obligations-frontend | `/health/all` | `X-Health-Check-Token` |
| packaging-waste-proxy | `/health/all` | `X-Health-Check-Token`; the gateway may also require OAuth |

The packaging frontend supports `?deep=true` to include the gateway's downstream report. Configure the full externally reachable URL if gateway routing adds a prefix. These paths come from `Program.cs` and `HealthChecks/HealthAllOptions.cs` in epr-packaging-frontend, `src/server/routes/health` in waste-obligations-frontend, and `Utils/Health/WebApplicationExtensions.cs` in both backends.

## Authentication

Configuration uses the standard .NET providers and binder, in order: `appsettings.json`, `.env` (through [DotNetEnv](https://github.com/tonerdo/dotnet-env)), then process environment variables. Later providers override earlier values. Double underscores map to nested settings, including service dictionary keys:

```dotenv
Services__epr-packaging-frontend__ApiKey__Value='frontend-health-token'
Services__waste-obligations__OAuth__ClientId='gateway-client-id'
Services__waste-obligations__OAuth__ClientSecret='gateway-client-secret'
```

These override `Services:epr-packaging-frontend:ApiKey:Value`, `Services:waste-obligations:OAuth:ClientId` and `Services:waste-obligations:OAuth:ClientSecret` directly. Keep credentials in `.env`; the committed `appsettings.json` has empty credential values. Service names remain stable when settings are reordered. Any other setting can also be overridden, for example `Services__epr-packaging-frontend__Url` or `TimeoutSeconds`.

Omit `ApiKey` and `OAuth` for an anonymous service. Configure either or both per service:

- `ApiKey.Value`: health API key. `HeaderName` defaults to `X-Health-Check-Token`.
- `OAuth.TokenUrl`: token endpoint for the gateway's identity provider.
- `OAuth.ClientId` and `ClientSecret`: client credentials.

`.env` supports standard dotenv assignments, comments and quoting. Use single quotes for literal secrets containing `$` or `#`. Hyphenated service names work in `.env`; when supplying them directly from a shell, use `env 'Services__waste-obligations__OAuth__ClientSecret=value' dotnet run --project tools/healthcheck` because shell assignment syntax does not accept hyphens. A `.env` file is optional when credentials are supplied by the process environment. Loading it does not modify the process environment.

OAuth uses `grant_type=client_credentials` with form-encoded `client_id` and `client_secret` in the POST body. A token is acquired separately for each configured OAuth service and sent as a Bearer token on its GET. Providers requiring HTTP Basic client authentication are not supported. API keys are sent only on the health GET. Redirects and cookies are disabled. TLS certificate validation remains enabled.

## Results

Before each check starts, the CLI prints the service name, GET endpoint (without its query string) and timeout, then flushes the output immediately. In `--json` mode this progress goes to stderr so stdout remains valid JSON.

Each service produces PASS/FAIL, its reported status, HTTP status and elapsed milliseconds, followed by dependency statuses. Nested `results` inside dependency `response` objects are included. JSON output contains the same information in an array. Response descriptions, exception messages, token payloads and raw response bodies are not printed.

A check passes only for a successful HTTP response with JSON `status: "Healthy"` and healthy reported dependencies. Degraded, unhealthy, missing/unknown status, malformed JSON, redirects, authentication failures and network failures fail the check. A `503` JSON body is still inspected for dependency statuses.

Checks run sequentially and continue after individual failures. `TimeoutSeconds` (default 5, range 1–5) covers token acquisition and the health GET together for each service. A check that exceeds its timeout is reported as unhealthy, and the CLI moves on to the next service. Responses are limited to 1 MiB. Ctrl+C cancels the run.

Exit codes: **0** all healthy; **1** any failed/degraded check; **2** invalid arguments/configuration or missing credentials; **130** cancelled. All configuration and credentials are validated before requests start.

## Build and test

The tool and its tests have their own solution, `tools/healthcheck/healthcheck.slnx`. They are independent of the main API solution and Docker build.

```sh
dotnet build tools/healthcheck/healthcheck.slnx
dotnet test --test-modules tools/healthcheck/tests/HealthCheck.Tests/bin/Debug/net10.0/HealthCheck.Tests.dll --no-build
dotnet publish tools/healthcheck/HealthCheck.csproj -c Release -o /tmp/healthcheck
dotnet /tmp/healthcheck/HealthCheck.dll /path/to/appsettings.json
```
