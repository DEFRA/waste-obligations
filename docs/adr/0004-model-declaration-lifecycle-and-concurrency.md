# ADR-0004: Model declaration lifecycle and concurrency explicitly

**Status:** Accepted (retrospective)
**Confidence:** High
**Source PRs:** [#34](https://github.com/DEFRA/waste-obligations/pull/34), [#50](https://github.com/DEFRA/waste-obligations/pull/50), [#59](https://github.com/DEFRA/waste-obligations/pull/59), [#63](https://github.com/DEFRA/waste-obligations/pull/63), [#69](https://github.com/DEFRA/waste-obligations/pull/69), [#154](https://github.com/DEFRA/waste-obligations/pull/154)

## Decision

Compliance declarations have a controlled status lifecycle, embedded business audit history, and optimistic concurrency. The service, rather than the public API client, owns the stored version used for concurrency checks.

## Current definition

Creation submits a declaration, sets version `1`, and appends a `Submitted` audit entry. Valid transitions are `Submitted → Accepted`, `Submitted → Cancelled`, and `Accepted → Cancelled`. A transition appends a timestamped audit entry with the actor and an optional reason.

Update and delete filters include the current internal `Version`; a successful update increments it. A version conflict returns `409 Conflict`, which a client may retry. A requested but invalid state transition returns `422 Unprocessable Entity`, which retrying cannot correct. `IsRegulation43Compliant` and `ObligationStatus` are declaration facts, not alternative lifecycle mechanisms.

## Consequences

- The public request does not carry an entity version, so stale writes are detected after the service reads the current document.
- `Version` is distinct from `SchemaVersion`; the latter governs payload shape, not concurrency.
- Status changes create business history and, through ADR-0007, publishable change history.

## Evidence

PR #50 introduced the embedded audit structure. PR #59 introduced PATCH status updates and internal optimistic concurrency. PR #63 explicitly corrected the `409`/`422` distinction. PR #154 added the `Accepted → Cancelled` transition. The entity's `CanTransition` and service update filters are the current executable definition.
