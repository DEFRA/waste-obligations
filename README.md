# Waste Obligations

An API for managing obligation data in relation to EPR.

## Prerequisites

- .NET 10
- Docker

## Setup process

- Ensure .NET 10 SDK is installed
- Ensure a container runtime is installed

### Running locally via Docker

```bash
docker compose up -d --wait
```

### Running via .NET

Mongo will be needed and can be started as follows:

```bash
docker compose up mongodb -d --wait
```

Start the API as follows:

```bash
dotnet run --project ./src/Api --launch-profile Api
```

The same port (8080) is used for launch profile and Docker compose configuration, therefore only one can run at any one time. 

### Documentation

API documentation can be viewed at http://localhost:8080/documentation/index.html once the service is running.

Additional project documentation:

- [Analytics compliance declaration events](docs/analytics-compliance-declaration-events.md)

## Concepts

### Compliance declaration analytics events

Compliance declaration writes are captured in the same transaction as the declaration change. A background analytics processor claims undispatched changes, serialises them as analytics events using the versioned entity schema, and publishes them to the analytics SNS topic.

The default analytics dispatch process name is `analytics`. Create events are published as `submission.created` with an `insert` operation, update events are published as `submission.amended` with an `update` operation, and delete events are published as `submission.removed` with a `delete` operation.

Analytics messages are JSON by default. If a serialised message would exceed the SNS message size budget, the message body is gzip-compressed and base64-encoded, and the SNS message includes `Content-Encoding: gzip+base64`.

### Stopping and clearing local resources

```bash
docker compose down
```

To remove local data:

```bash
docker compose down -v --remove-orphans
```

## Tests

Tests with the `IntegrationTests` trait require additional local dependencies - either the API running in Docker or Mongo.

Running unit tests:

```bash
dotnet build tests/Api.Tests/Api.Tests.csproj
dotnet test --test-modules tests/Api.Tests/bin/Debug/net10.0/Api.Tests.dll --no-build
```

Running integration tests with dependencies:

```bash
dotnet build tests/Api.IntegrationTests/Api.IntegrationTests.csproj
dotnet test --test-modules tests/Api.IntegrationTests/bin/Debug/net10.0/Api.IntegrationTests.dll --no-build
```

Running all tests (with the integration-test dependencies available):

```bash
dotnet build
dotnet test --test-modules tests/Api.Tests/bin/Debug/net10.0/Api.Tests.dll --no-build
dotnet test --test-modules tests/Api.IntegrationTests/bin/Debug/net10.0/Api.IntegrationTests.dll --no-build
```

### Govuk Notify

The nullable [email delivery cutover](docs/email-delivery-cutover.md) defaults to null, preserving current email behaviour. Configure the shared future UTC boundary only when the replacement Notifications producers are ready.

The `GovukNotifyTests` integration tests can run against Govuk Notify if you provide an API Key.

Set the `GOVUKNOTIFY_APIKEY` env var in a terminal and then run the integration tests:

```
export GOVUKNOTIFY_APIKEY=[replace with key]
dotnet build tests/Api.IntegrationTests/Api.IntegrationTests.csproj
dotnet test --test-modules tests/Api.IntegrationTests/bin/Debug/net10.0/Api.IntegrationTests.dll --no-build
```

The test runner writes console output so use of the API Key can be seen.


## Code quality

SonarQube cloud is configured and all Defra rules are mandated. 

See https://sonarcloud.io/project/overview?id=DEFRA_waste-obligations for project information.

## Dependency management

Dependabot is configured for ongoing dependency management.

See [dependabot.yml](.github/dependabot.yml) for group configuration.

## Build pipeline

- [Pull requests](.github/workflows/check-pull-request.yml)
  - Run all tests
  - Build Docker image
  - Check image with Trivy
  - Sonar
  - Run Waste Obligations journeys for trusted pull requests
- [Publish](.github/workflows/publish.yml)
  - Merge PR to main
  - Build Docker image and publish to CDP
  - Sonar

## Scheduled jobs

Implement `IScheduledJob.Run(CancellationToken)` in `src/Api/ScheduledJobs/<Name>Job.cs`,
using the namespace `Defra.WasteObligations.Api.ScheduledJobs`. The configuration
entry name selects the implementation by convention: `ObligationCalculation` loads
`Defra.WasteObligations.Api.ScheduledJobs.ObligationCalculationJob` from the API assembly.
`Program.cs` registers all configured jobs with
`builder.Services.AddScheduledJobs(builder.Configuration.GetSection("ScheduledJobs"))`.
Each entry accepts `Enabled` (default `false`) and `Cron` (a five-field UTC expression,
required when enabled). Disabled jobs register no worker and do not access MongoDB.
An enabled job with a missing implementation or invalid schedule prevents startup.

The obligation calculation job is disabled in `appsettings.json`. Set
`ScheduledJobs__ObligationCalculation__Enabled=true` to enable it at host startup.
Its default schedule is `0 1 * * *` (01:00 UTC); override it with
`ScheduledJobs__ObligationCalculation__Cron`. The job creates a pending Mongo instruction for all submitters in the current obligation year.
January uses the previous year; the year changes on 1 February. Calculation processing
and result storage are left for later commits. These environment values
must be set on the API container in the target environment. No deployed configuration
or journey settings change is needed while the job remains disabled.

Each host waits for the next occurrence. A per-job Mongo lease allows one host to
execute at a time, with renewal every 20 seconds and a one-minute expiry. The last
started occurrence is stored separately, so another host cannot repeat it after
completion. Failed attempts are logged and consumed; the next occurrence can run.
Missed occurrences while a host is stopped are not replayed.

Jobs must honour cancellation. Shutdown or lease loss cancels the job; the worker
waits for it to finish before releasing its lease or attempting another run.
Renewal continues while shutdown drains the job. An expired lease permits recovery
after a crash, but cannot guarantee zero overlap during a host pause or network
partition. Keep job names stable across hosts and deployments: the name identifies
both the lease and occurrence history in the private `_scheduled_job_leases` and
`_scheduled_job_state` collections.

Obligation calculation instructions are stored in `_obligation_calculation_instructions`
with an ID, obligation year, nullable submitter GUID, status and creation timestamp.
A null submitter means all submitters for the year. Creation returns the existing
pending or running instruction for the same year and submitter; whole-year and
submitter instructions do not block one another. Migration 021 adds a partial unique
index to enforce this across hosts. Creation waits for startup migrations before
writing, and no existing documents or analytics schemas are changed.

The shared instruction service accepts an explicit year and optional submitter ID
for future manual requests. This slice only creates instructions: it does not process
them or expose a management endpoint. Instructions remain pending and prevent matching
new instructions until processing is implemented, so leave the job disabled in deployed
environments for now. The scheduler lease protects instruction creation; it does not
represent the lifetime of the calculation instruction.

## System health checks

The [.NET health-check CLI](tools/healthcheck/README.md) checks configurable services and their dependencies. Configure service addresses in `tools/healthcheck/appsettings.json` and credentials in the Git-ignored `tools/healthcheck/.env` file.

## CDP

Review CDP documentation and process for relevant portal operations.

## Journey tests

For repository-secret setup, see [Journey tests in Confluence](https://eaflood.atlassian.net/wiki/spaces/EDIA/pages/6597640835/Journey+tests).

Trusted pull requests and manually dispatched **Check Pull Request** runs execute the Waste Obligations journey suite in a separate job. The job builds the selected source revision locally; it does not publish a test image. If a branch with the same name exists in `waste-obligations-journey-tests`, that branch supplies the journeys and dedicated CI stack; otherwise the suite uses `main`.

The required GitHub Actions secrets and OIDC/CDP-role prerequisite are documented in the [journey-test CI contract](https://github.com/DEFRA/waste-obligations-journey-tests#github-actions-secrets). Configure that shared contract in this repository, or use organisation-level secrets restricted to the journey and service repositories. Fork pull requests do not run the job because repository secrets are unavailable to them.

## Github

The following secrets are configured in the repository in Github:

GOVUKNOTIFY_APIKEY - the correct GOV.UK Notify Test API Key used by the API integration tests. Ensure it remains a Test API Key as per guidance https://docs.notifications.service.gov.uk/java.html#test.

GOVUK_NOTIFY_API_KEY - a separate, format-valid dummy key used only by the journey-test stack's WireMock-backed GOV.UK Notify mapping. It must not replace `GOVUKNOTIFY_APIKEY` and does not call GOV.UK Notify. Its shared setup is documented in the [journey-test CI contract](https://github.com/DEFRA/waste-obligations-journey-tests#github-actions-secrets).

Also ensure it's defined in the main repository secrets and also the Dependabot secrets.

SONAR_TOKEN - the API Key for SonarCloud https://sonarcloud.io/project/overview?id=DEFRA_waste-obligations

## Licence Information

THIS INFORMATION IS LICENSED UNDER THE CONDITIONS OF THE OPEN GOVERNMENT LICENCE found at:

http://www.nationalarchives.gov.uk/doc/open-government-licence/version/3

### About the licence

The Open Government Licence (OGL) was developed by the Controller of Her Majesty's Stationery Office (HMSO) to enable information providers in the public sector to license the use and re-use of their information under a common open licence.

It is designed to encourage use and re-use of information freely and flexibly, with only a few conditions.
