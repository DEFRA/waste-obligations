# ADR-0012: Operate with layered health and observability

**Status:** Accepted (retrospective)
**Confidence:** Medium
**Source PRs:** [#1](https://github.com/DEFRA/waste-obligations/pull/1), [#2](https://github.com/DEFRA/waste-obligations/pull/2), [#37](https://github.com/DEFRA/waste-obligations/pull/37), [#38](https://github.com/DEFRA/waste-obligations/pull/38), [#113](https://github.com/DEFRA/waste-obligations/pull/113), [#156](https://github.com/DEFRA/waste-obligations/pull/156), [#157](https://github.com/DEFRA/waste-obligations/pull/157), [#172](https://github.com/DEFRA/waste-obligations/pull/172)

## Decision

Separate lightweight service readiness from diagnostic dependency health, and instrument meaningful external operations with structured logs and metrics.

## Current definition

`/health` is the anonymous readiness route. `/health/authorized` exposes the same readiness predicate to authenticated callers. `/health/all` is the diagnostic route: it runs extended checks and returns structured per-check status, description, exception and data. Extended checks cover downstream OAuth/API connectivity, Account Backend, Waste Organisations, Gov.uk Notify and analytics SNS/SQS.

The service emits metrics for analytics dispatch and email delivery. The obligations read path records a structured latency breakdown spanning parallel organisation lookup, OAuth token acquisition, PRN calculation/source call and response mapping; it logs that breakdown only after successful responses complete.

## Consequences

- Routine platform health does not require every external dependency to be healthy.
- Operators have a diagnostic endpoint for dependency configuration and identity/token details without adding those details to public business responses.
- Latency investigations can target a named segment instead of treating the request as one opaque duration.

## Evidence

The initial service established health/metrics foundations. PR #37 added downstream Azure/PRN diagnostic health, #156 expanded OAuth diagnostics, and #157 added the authenticated readiness route. PR #113 added email and analytics metrics; #172 added the obligations latency breakdown. The exact readiness-versus-diagnostic intent is inferred from the retained endpoint tags and routes, hence Medium confidence.
