# Agents Guidelines

## Coding conventions
- Do not use the `Async` suffix for asynchronous methods
- Add blank line before return statement
- Use constants for values that are used more than once; inline values that are only used once
- Lint files changed/created using "dotnet csharpier format ."
- Name expressions with x => x. syntax where possible
- Specify variables as const in tests where possible
- Use collection expressions where possible
- When enums are used as variables, inline them instead of creating a property
- Do not use Arrange Act Assert comments in tests
- Use _camelCase for private instance fields
- Prefer AwesomeAssertions for assertions; where `Should().NotBeNull()` provides nullable flow information, do not add redundant null suppression operators or extra null guards
- Place new appsettings.json (and related environment variant files) config sections at the bottom of existing settings

## Change iterations
- When changing entity or DTO types, inspect fixtures in tests and assess changes needed
- Work backwards through tests to assess changes
- In tests, prefer the fixtures in the Testing support project for repeated valid entity, DTO, and service response shapes; direct instantiation is fine for intentionally malformed/null payloads or small one-off values where a fixture would add noise
- Fixture location should follow the `tests/Testing/Fixtures` folder taxonomy: DTO fixtures in `Dtos`, entity fixtures in `Entities`, and service integration response fixtures in folders named for that integration
- Attempt to mask use of ToString where possible
- Check work has been successful by building the solution
- Run Api.Tests after any change
- Run Api.IntegrationTests after any change

## Mongo migrations
- Mongo migrations run on API host startup, so they must be guarded by a distributed Mongo lease before any migration work is attempted
- Migrations must be compatible with the previous deployed application version because outgoing hosts can continue processing requests during rollout
- Use an expand/backfill/contract rollout for breaking Mongo changes, including required fields, field renames/removals, incompatible type changes, strict validation, dropped indexes, or unique constraints
- Prefer adding indexes, optional fields, and permissive validation first; backfill existing data; then enforce stricter validation or remove old structures in a later deployment after old hosts are drained
- Keep historical audit events unchanged when schema versions move forward

## Build guidance
- In the sandbox environment, avoid plain `dotnet build` because it can hang or take significantly longer due to workload notification/build-server delays
- Build with `DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet build waste-obligations.slnx --no-restore -p:OpenApiGenerateDocuments=false -m:1 -nodeReuse:false --disable-build-servers -v:minimal`
- If a build is unexpectedly slow, stop it, run `dotnet build-server shutdown`, and retry the sandbox build command above

## Test guidance
- Run Api.Tests with `DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore -p:OpenApiGenerateDocuments=false -m:1 -nodeReuse:false --disable-build-servers -v:minimal`
- In the sandbox environment, Api.Tests may need escalation because VSTest binds a local socket for test host communication

## Integration tests
- Keep integration tests focused on integration boundaries. Use them to prove real components are wired together and observable side effects happen; put detailed formatting, serialisation, and field-by-field assertions in fast unit tests where possible.
- Run the local environment with `docker compose up --build -d`
- Run Api.IntegrationTests with `DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet test tests/Api.IntegrationTests/Api.IntegrationTests.csproj --no-restore -p:OpenApiGenerateDocuments=false -m:1 -nodeReuse:false --disable-build-servers -v:minimal`
- Stop the local environment with `docker compose down -v --remove-orphans`
- In the sandbox environment, Api.IntegrationTests need escalation because VSTest binds a local socket and the tests access Docker Compose services
