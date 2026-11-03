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

Implement `IScheduledJob.Run(IClientSessionHandle, CancellationToken)` in `src/Api/ScheduledJobs/<Name>Job.cs`,
using the namespace `Defra.WasteObligations.Api.ScheduledJobs`. The configuration
entry name selects the implementation by convention: `ObligationCalculation` loads
`Defra.WasteObligations.Api.ScheduledJobs.ObligationCalculationJob` from the API assembly.
`Program.cs` registers all configured jobs with
`builder.Services.AddScheduledJobs(builder.Configuration.GetSection("ScheduledJobs"))`.
Each entry accepts `Enabled` (default `false`) and `Cron` (a five-field UTC expression,
required when enabled). When both scheduling and processing are disabled, the entry registers no worker and does not access MongoDB.
An enabled job with a missing implementation or invalid cron syntax prevents startup.
A valid expression with no future occurrence logs a warning and stops that worker.

The obligation calculation job is disabled in `appsettings.json`. Set
`ScheduledJobs__ObligationCalculation__Enabled=true` to enable it at host startup.
Its default schedule is `0 1 * * *` (01:00 UTC); override it with
`ScheduledJobs__ObligationCalculation__Cron`. The job creates a pending Mongo instruction for all submitters in the current obligation year.
January uses the previous year; the year changes on 1 February. Actual calculation
rules and result storage are left for later commits. These environment values
must be set on the API container in the target environment. No deployed configuration
or journey settings change is needed while scheduling and processing remain disabled.

Each host waits for the next occurrence. A per-job Mongo lease allows one host to
execute at a time, with renewal every 20 seconds and a one-minute expiry.
For the obligation calculation, instruction creation and the occurrence marker
commit together in a Mongo transaction. Hosts that observe an occurrence retry
lease contention every five seconds. Failed handoffs use jittered exponential
backoff from five seconds up to one minute. Both retry until the handoff succeeds
or the next occurrence is due. A retry checks the committed marker, even if the
instruction has already completed. Retry state is held in memory: if every host
that observed an occurrence stops before the handoff commits, a replacement host
waits for the next occurrence. Missed occurrences while all hosts are stopped are
not replayed. Scheduled jobs perform only their durable MongoDB handoff using
the supplied session; the transaction callback may be retried. External calls and
other nontransactional effects belong in the processing worker after commit.

Jobs must honour cancellation. Shutdown or lease loss cancels the job; the worker
waits for it to finish before releasing its lease or attempting another run.
Renewal continues while shutdown drains the job. Lease calls request cancellation
after ten seconds and drain before the worker moves on; a late successful acquisition
is released without running the job. A stalled renewal cancels the job, and release
failures are logged so the lease can expire naturally. These are cooperative
cancellation deadlines, not permission to abandon an in-flight Mongo operation.
An expired lease permits recovery
after a crash, but cannot guarantee zero overlap during a host pause or network
partition. Keep job names stable across hosts and deployments: the name identifies
both the lease and occurrence history in the private `_scheduled_job_leases` and
`_scheduled_job_state` collections.

Obligation calculation instructions are stored in `_obligation_calculation_instructions`
with an ID, obligation year, nullable submitter GUID, status and creation timestamp.
A null submitter means all submitters for the year. Creation returns the existing
pending or running instruction for the same year and submitter; whole-year and
submitter instructions do not block one another. Migration 021 adds a partial unique
index to enforce this across hosts. Instruction creation assumes that index already
exists and does not wait for migrations. Apply the migrations before enabling the
job or adding another producer. The target MongoDB deployment must support `$in`
in partial index filters (MongoDB 6.0 or later) and support transactions, including
creating the scheduler state collection on its first handoff. Confirm the deployed
version and topology before enabling. The transaction shares the configured
`Mongo:TransactionTimeoutSeconds` budget (five seconds by default). No existing
documents or analytics schemas are changed. Outside a transaction, creation retries
duplicate-key collisions up to five attempts. In a transaction, a collision aborts
the attempt and the scheduler retries the entire handoff.

When no matching pending or running instruction exists, creation always inserts a new
document with a new ID; completed and failed instructions are retained as history.
Migration 021 adds a partial TTL index that removes completed and failed instructions
365 days after their creation timestamp. Pending and running instructions never expire.
MongoDB performs TTL cleanup periodically, so deletion is not immediate at the cutoff.

An admin can queue or reuse an active instruction with `POST /admin/obligation-calculations?obligationYear=2026&submitterId=<guid>`. The obligation year is required; omit the submitter ID to select all submitters. Processing must be enabled to consume the instruction. Scheduling and processing are configured together under `ScheduledJobs:ObligationCalculation`, but can be enabled independently. Both default to disabled:

```json
"ObligationCalculation": {
  "Enabled": false,
  "Cron": "0 1 * * *",
  "Processing": {
    "Enabled": false,
    "PollIntervalSeconds": 10,
    "MaximumAttempts": 3
  }
}
```

Set `ScheduledJobs__ObligationCalculation__Processing__Enabled=true` to start processing, and use `ScheduledJobs__ObligationCalculation__Processing__PollIntervalSeconds` to change the idle/error polling delay. Each host processes one instruction at a time, then checks immediately for more work. Three hosts can process three different instructions concurrently. Background execution does not block host startup.

The same `AddScheduledJobs` registration discovers `<Name>Processor` and `<Name>WorkSource` in the scheduled-jobs namespace when processing is enabled. The processor implements `IScheduledJobProcessor<TWork>` and the matching work source implements `IScheduledJobWorkSource<TWork>`. Disabled or omitted processing needs neither implementation. The work source finds work and controls persisted state; the shared worker owns execution and passes the payload to the processor.

The obligation work source selects the oldest pending or running instruction. The worker acquires a renewable lease keyed by job name and instruction ID, rechecks that it is active, marks it running, invokes the processor, and marks it completed only after successful processing. Busy candidates are skipped so another instruction can run. Running instructions can be recovered after lease expiry. The placeholder processor only logs that no calculation was performed.

A processor failure logs the instruction ID and atomically increments its persisted failure count. Below the limit, the instruction stays `Running` and is skipped for the rest of the polling pass, allowing other work to run before retry. On the third failure by default, it becomes `Failed` and is never selected again. Configure the limit with `ScheduledJobs__ObligationCalculation__Processing__MaximumAttempts` (minimum one). Counts survive host changes; older instructions without a count start at zero. Shutdown and lease-loss cancellation do not count as processing failures. A later schedule can create a fresh instruction for that scope.

Migration 021 creates the admission, retention and work-selection indexes, plus a TTL index retaining expired processing leases for 365 days. Processing leases use the private `_scheduled_job_processing_leases` collection. Apply migrations before enabling either scheduling or processing. Lease renewal and cancellation use the same lifecycle as scheduled execution; expired leases still permit overlap during host pauses or network partitions. Real calculations must make their side effects idempotent.

Mongo admission is independent of delivery. A future SQS message should carry an existing instruction ID; receiving or redelivering it must load that instruction rather than create another one. The active-scope unique index continues to block duplicate admission, and instruction ownership controls processing. Reliable queue publication and acknowledgement would need their own implementation. No SQS integration, calculation or result persistence is included here. Keep both switches disabled until placeholder completion is explicitly wanted.

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
