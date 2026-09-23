# 0016: Run migrations through a renewable background lease

**Status:** Accepted—retrospective.

## Context

PR #220 records a migration that delayed readiness for more than 100 minutes during rollout.

## Decision

Start the host promptly and run migrations through a background service with a renewable Mongo lease, bounded attempts, and retry.

## Rationale basis

**Documented:** #220 gives the rollout incident, immediate listening, renewable lease, bounded attempt, and no-version-preemption rationale. **Corroborated:** service/lease/runner/options. **Inference:** TTL leasing coordinates cooperative hosts but is not fencing.

## Consequences and evolution

Readiness does not prove migration completion. Breaking changes still require compatible rollout planning.

## Evidence

- #220 `d227e793`.
- `src/Api/Data/MongoMigrationService.cs`, `MongoMigrationLeaseService.cs`, `MongoMigrationRunner.cs`, `MongoMigrationOptions.cs`.
