# 0015: Pace recurring hydration and finite backfill

**Status:** Accepted—retrospective.

## Context

Materialised obligations require annual hydration and finite backfill under downstream-capacity constraints.

## Decision

Coordinate hydration/backfill through the shared lease and persist adaptive request-per-minute pacing. Keep work nontransactional but safe to retry at the work-item boundary.

## Rationale basis

**Documented:** #225 establishes annual policy, finite backfill, shared lease, and adaptive pacing. **Corroborated:** current registrations. **Inference:** persisted pacing outlives a single worker process.

## Consequences and evolution

This has incremental progress, not eligibility generation’s all-or-nothing publication. ADR 0009 does not generalise to it.

## Evidence

- #225 `39ae8281`.
- `src/Api/Services/OrganisationObligations/ServiceCollectionExtensions.cs`.
