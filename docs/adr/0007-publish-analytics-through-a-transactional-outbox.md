# ADR-0007: Publish analytics through a transactional outbox

**Status:** Accepted (retrospective)
**Confidence:** High
**Source PRs:** [#99](https://github.com/DEFRA/waste-obligations/pull/99), [#100](https://github.com/DEFRA/waste-obligations/pull/100), [#102](https://github.com/DEFRA/waste-obligations/pull/102), [#104](https://github.com/DEFRA/waste-obligations/pull/104), [#106](https://github.com/DEFRA/waste-obligations/pull/106), [#113](https://github.com/DEFRA/waste-obligations/pull/113)

## Decision

Record declaration mutations and their publishable analytics events in the same Mongo transaction. Publish the resulting outbox records asynchronously to SNS from a lease-protected background processor.

## Current definition

Create, update and delete write an `AuditEvent` alongside the declaration mutation. The event has a globally increasing sequence, an event ID, entity identity, operation, event type, version, schema version, actor, trace ID and immutable `before`/`after` snapshots. Create, update and delete map respectively to `submission.created`, `submission.amended` and `submission.removed`.

The analytics processor claims the `analytics` process lease, reads oldest undispatched/due failed events, serialises against the event's embedded schema version, then publishes to SNS. It records `Dispatched`, `Failed` or `DeadLettered` per process with attempt and retry metadata. Oversized SNS bodies are gzip/base64 encoded when that fits; delivery is at least once, so consumers must de-duplicate by event ID or sequence.

## Consequences

- A successful API mutation cannot lose its outbox record; failed publication does not roll back the committed declaration.
- Consumers receive an integration envelope, not an internal Mongo document.
- Background process coordination and migrations use private Mongo lease collections, not request-path state.

## Evidence

PR #99 explicitly introduced the generic event envelope, transactional recording, asynchronous dispatch, retries, leases and SNS handling. PRs #100, #102 and #104 were merged into that analytics branch before it reached `main`; they refined the dispatcher, schema serialisation, traceability and failure isolation. The event-flow documentation and `AGENTS.md` retain the operational invariants.
