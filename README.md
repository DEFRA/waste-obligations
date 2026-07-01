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
docker compose up -d
```

### Running via .NET

Mongo will be needed and can be started as follows:

```bash
docker compose up mongodb -d
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

Running tests without dependencies:

```bash
dotnet test --filter "Category!=IntegrationTests"
```

Running tests with dependencies:

```bash
dotnet test --filter "Category=IntegrationTests"
```

Running all:

```bash
dotnet test
```

### Govuk Notify

The `GovukNotifyTests` integration tests can run against Govuk Notify if you provide an API Key.

Set the `GOVUKNOTIFY_APIKEY` env var in a terminal and then run the integration tests:

```
export GOVUKNOTIFY_APIKEY=[replace with key]
dotnet test --filter "Category=IntegrationTests" --logger "console;verbosity=detailed"
```

Note the command above to run the tests allows console output so use of the API Key can be seen.


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
- [Publish](.github/workflows/publish.yml)
  - Merge PR to main
  - Build Docker image and publish to CDP
  - Sonar

## CDP

Review CDP documentation and process for relevant portal operations.

## Github

The following secrets are configured in the repository in Github:

GOVUKNOTIFY_APIKEY - the API Key for Govuk Notify integration tests. Ensure it's a Test API Key as per guidance https://docs.notifications.service.gov.uk/java.html#test.

Also ensure it's defined in the main repository secrets and also the Dependabot secrets.

SONAR_TOKEN - the API Key for SonarCloud https://sonarcloud.io/project/overview?id=DEFRA_waste-obligations

## Licence Information

THIS INFORMATION IS LICENSED UNDER THE CONDITIONS OF THE OPEN GOVERNMENT LICENCE found at:

http://www.nationalarchives.gov.uk/doc/open-government-licence/version/3

### About the licence

The Open Government Licence (OGL) was developed by the Controller of Her Majesty's Stationery Office (HMSO) to enable information providers in the public sector to license the use and re-use of their information under a common open licence.

It is designed to encourage use and re-use of information freely and flexibly, with only a few conditions.
