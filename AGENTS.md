# Agents Guidelines

## Coding conventions
- Do not use the `Async` suffix for asynchronous methods
- Add blank line before return statement
- Prefer typed `Results.[method]()` helpers over `Results.Problem()` for endpoint responses; reserve `Results.Problem()` for cases without an appropriate typed helper
- Use constants for values that are used more than once; inline values that are only used once
- Use camelCase for constants declared within methods
- Lint files changed/created using "dotnet csharpier format ."
- Name expressions with x => x. syntax where possible
- Specify variables as const in tests where possible
- Use collection expressions where possible
- Use object initializers where possible
- When enums are used as variables, inline them instead of creating a property
- Do not use Arrange Act Assert comments in tests
- Use _camelCase for private instance fields
- Prefer AwesomeAssertions for assertions; where `Should().NotBeNull()` provides nullable flow information, do not add redundant null suppression operators or extra null guards
- Keep assertion style consistent within a test or helper; avoid introducing local variables solely for one assertion when surrounding assertions access the same object inline
- When fixing Sonar or analyzer findings in tests, prefer keeping values local to the assertion or test where possible; do not hoist values to test class level solely to satisfy a warning unless they are genuinely shared
- Place new appsettings.json (and related environment variant files) config sections at the bottom of existing settings

## Change iterations
- Integration clients should return their integration response models rather than public API DTOs; map to API DTOs in the consuming endpoint or application service
- Before adding an endpoint, request DTO, validation rule, serialisation converter, or OpenAPI customisation, compare the nearest existing implementation. If the change needs a one-off pattern or would alter an established request, validation, error-response, or documentation convention, pause and ask the user before introducing it.
- When changing entity or DTO types, follow the persisted entity and schema change workflow below, then inspect fixtures in tests and assess changes needed
- Work backwards through tests to assess changes
- In tests, prefer the fixtures in the Testing support project for repeated valid entity, DTO, and service response shapes; direct instantiation is fine for intentionally malformed/null payloads or small one-off values where a fixture would add noise
- Fixture location should follow the `tests/Testing/Fixtures` folder taxonomy: DTO fixtures in `Dtos`, entity fixtures in `Entities`, and service integration response fixtures in folders named for that integration
- Attempt to mask use of ToString where possible
- Check work has been successful by building the solution
- Run Api.Tests after any change
- Run Api.IntegrationTests after any change

## Persisted entity and schema changes
- Treat a change to a persisted entity, including any nested entity such as `User`, as a possible Mongo storage and analytics contract change; do not assess only the root entity file
- Do not confuse an entity's `Version`, which is used for optimistic concurrency, with its `SchemaVersion`, which identifies the persisted and analytics payload shape
- Before changing an entity, trace the affected field through the Mongo BSON shape, DTOs, validation, mappings, fixtures, embedded JSON schemas, audit-event `before` and `after` payloads, analytics serialisation, OpenAPI snapshots, integration snapshots, migrations, and documentation
- Use major and minor schema versions only; patch schema versions are not used in this repository
- Do not bump the schema version for an implementation-only change that cannot alter persisted BSON or the analytics payload
- Use a new minor version for a backwards-compatible additive change, such as adding an optional or nullable property, widening an accepted value set, or adding an optional object variant; legacy documents and events must remain representable by the new schema
- Use a new major version for a breaking change, such as removing, renaming, or moving a property; making an optional or nullable property required or non-nullable; changing a type, format, BSON/JSON representation, or meaning; narrowing constraints or enum values; or otherwise making a previously valid payload invalid
- When compatibility is uncertain, use a new major version or split the change into an expand/backfill/contract rollout rather than redefining an existing contract
- Treat every published or persisted schema file as immutable. Copy the latest schema to a new versioned file, update its `$id`, and keep every older schema embedded so undispatched historical audit events can still be serialised with the schema version they recorded
- Update the entity's `SchemaVersionValue` only after the new schema file exists, and search the repository for every hard-coded old version, schema filename, qualified analytics version, and documentation link

Follow this order for a persisted entity shape change:

1. Classify the change as no schema change, minor, or major, and decide whether the rollout needs separate expand, backfill, and contract deployments that remain compatible with the previous application version.
2. Update the entity and all affected nested entities, DTOs, validators, mappings, and shared fixtures. Keep new persisted fields optional during a mixed-version rollout unless old hosts can safely read and write them.
3. Copy the current embedded schema to the chosen new version, update only the new file and its `$id`, and update `SchemaVersionValue`. Do not edit or remove the previous schema.
4. Add a new, monotonically ordered Mongo migration when existing documents need a new shape or `schemaVersion`; never edit a migration that may already have run in an environment.
5. Implement the migration against raw `BsonDocument` values so tests and migration logic represent the old stored shape rather than receiving defaults from the current entity serializer. Filter to the exact source versions, preserve values that a newer or concurrently running host may already have written, update the data and `schemaVersion` together, and make repeated execution safe.
6. Do not rewrite historical `AuditEvent` documents, their `before` or `after` snapshots, or their `schemaVersion`. The retained old embedded schema is what keeps those events publishable.
7. Add migration integration tests for each applicable starting state: the explicit previous version, a missing legacy `schemaVersion`, a pre-existing new field value, and an already-target-version document. Assert the transformed BSON and version, non-target data preservation, idempotent `UpAsync`, and `DownAsync` behaviour where reversal is supported.
8. Add or update unit tests that load both the new and retained prior schemas and serialise representative audit events with each version. Update entity, mapping, validation, endpoint, OpenAPI, analytics, and integration snapshots affected by the shape change.
9. Update `docs/compliance-declaration-end-to-end-event-flow.md` and `docs/analytics-compliance-declaration-events.md`, including current schema links, raw and qualified version examples, payload examples, and any migration or compatibility notes. Search all documentation for stale versions.
10. Format, build, and run Api.Tests and Api.IntegrationTests using the commands below.

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
- Do not change shared infrastructure settings, such as queue attributes or database-level configuration, from integration tests unless the test owns an isolated resource created specifically for that test.
- Run the local environment with `docker compose up --build -d`
- Run Api.IntegrationTests with `DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet test tests/Api.IntegrationTests/Api.IntegrationTests.csproj --no-restore -p:OpenApiGenerateDocuments=false -m:1 -nodeReuse:false --disable-build-servers -v:minimal`
- Stop the local environment with `docker compose down -v --remove-orphans`
- In the sandbox environment, Api.IntegrationTests need escalation because VSTest binds a local socket and the tests access Docker Compose services
