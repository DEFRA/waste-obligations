# ADR-0010: Retry bounded Mongo transactions

**Status:** Accepted (retrospective)
**Confidence:** High
**Source PRs:** [#107](https://github.com/DEFRA/waste-obligations/pull/107), [#160](https://github.com/DEFRA/waste-obligations/pull/160), [#161](https://github.com/DEFRA/waste-obligations/pull/161)

## Decision

Run declaration mutation/outbox writes in Mongo transactions with a service-owned time budget. Retry only known retryable transaction failures, including transient transaction errors and Mongo write-conflict error 112, within that same budget.

## Current definition

The Mongo context starts sessions with primary read preference, which is required for transaction compatibility in the deployed connection-string environment. Each transaction has a configured timeout and maximum commit time. Retryable failures use exponential delay plus jitter up to the configured retry count. Caller cancellation is preserved; exhausting the service timeout reports a transaction timeout rather than silently continuing.

## Consequences

- A genuine concurrent write can recover without asking the upstream caller to replay a non-idempotent operation.
- Arbitrary exceptions and caller-requested cancellation are not retried.
- Any operation in the transaction callback must be safe to execute again; declaration writes and audit-outbox writes share that transaction boundary.

## Evidence

PR #107 corrected a production/development mismatch around `secondaryPreferred` reads and transactions. PR #160 added the bounded transaction retry behaviour, and #161 expanded retry detection to the write-conflict forms observed under concurrent writes and moved the logic into the shared DB context. `MongoDbContext.ExecuteTransaction` is the current executable policy.
