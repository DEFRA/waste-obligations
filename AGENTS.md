# Agents Guidelines

## Coding conventions
- Do not use the `Async` suffix for asynchronous methods
- Add blank line before return statement
- Use constants for common strings when they are used more than once
- Lint files changed/created using "dotnet csharpier format ."
- Name expressions with x => x. syntax where possible
- Specify variables as const in tests where possible
- Use collection expressions where possible
- When enums are used as variables, inline them instead of creating a property
- Do not use Arrange Act Assert comments in tests
- Use _camelCase for private instance fields

## Change iterations
- When changing entity or DTO types, inspect fixtures in tests and assess changes needed
- Work backwards through tests to assess changes
- Attempt to mask use of ToString where possible
- Check work has been successful by building the solution

## Build guidance
- In the sandbox environment, avoid plain `dotnet build` because it can hang or take significantly longer due to workload notification/build-server delays
- Before building, run `dotnet build-server shutdown`
- Build with `DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet build waste-obligations.slnx --no-restore -p:OpenApiGenerateDocuments=false -m:1 -nodeReuse:false -v:minimal`
- If a build is unexpectedly slow, stop it, rerun `dotnet build-server shutdown`, and retry the sandbox build command above

## Integration tests
- Run the local environment with `docker compose up --build -d`
- Stop the local environment with `docker compose down -v --remove-orphans`
- The Api.IntegrationTests project can be run once the local environment is running
