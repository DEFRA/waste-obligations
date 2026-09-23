# Retrospective architecture decisions

Assessed against the current checkout on **23 September 2026**. “Accepted—retrospective” records an evidenced, retained implementation; it does not assert contemporaneous formal approval.

This catalogue supersedes the earlier local eight-record synthesis. It uses [PR #188](https://github.com/DEFRA/waste-obligations/pull/188) as an unmerged August candidate catalogue, reconciled with current code and later evidence through [PR #263](https://github.com/DEFRA/waste-obligations/pull/263).

| ADR | Decision |
| --- | --- |
| [0001](0001-obligations-response-boundary.md) | Keep obligations responses focused on obligations |
| [0002](0002-mongo-persistence-contract.md) | Maintain Mongo identifier, timestamp and query conventions |
| [0003](0003-declaration-submission-snapshots.md) | Preserve selected submission-time facts |
| [0004](0004-declaration-lifecycle-and-concurrency.md) | Enforce lifecycle rules with optimistic concurrency |
| [0005](0005-declaration-query-contract.md) | Constrain declaration search, sorting and paging |
| [0006](0006-derived-obligation-coverage.md) | Derive obligation coverage centrally |
| [0007](0007-versioned-persisted-and-analytics-schemas.md) | Preserve immutable versioned schemas |
| [0008](0008-transactional-analytics-outbox.md) | Deliver analytics through a transactional outbox |
| [0009](0009-shared-mongo-transaction-retry.md) | Centralise selected Mongo transaction retries |
| [0010](0010-best-effort-notifications.md) | Keep Notify delivery outside declaration success |
| [0011](0011-prn-integration-boundary.md) | Adapt PRN Common Backend behind a constrained contract |
| [0012](0012-source-specific-company-name.md) | Select company names by source registration type |
| [0013](0013-external-client-and-token-boundaries.md) | Separate request and background integration clients |
| [0014](0014-materialised-eligibility-generations.md) | Refresh eligibility through materialised generations |
| [0015](0015-obligations-hydration-and-backfill.md) | Pace recurring hydration and finite backfill |
| [0016](0016-leased-background-migrations.md) | Run migrations through a renewable background lease |
| [0017](0017-health-check-boundaries.md) | Maintain lightweight and aggregate health checks |
| [0018](0018-operational-data-access.md) | Restrict destructive and diagnostic data routes |
| [0019](0019-declaration-route-body-identity.md) | Validate route/body identity before persistence |
| [0020](0020-build-input-and-workflow-permissions.md) | Pin build inputs and separate workflow permissions |

Each record distinguishes documented rationale, code/history corroboration, and inference. See [PR coverage and candidate disposition](pr-coverage.md) for accountable grouping, exclusions, and evidence limits.

## Confidence gaps and open questions

- The business rationale for the CompanyName registration mapping is not documented.
- The intended trust boundary for `/health/all` is not established.
- Lease expiry does not fence an uncooperative migration or worker.
- Outbox dead-letter remediation/replay procedures were not evidenced.
- The reason for every schema version increment is not known.
- [PR #245](https://github.com/DEFRA/waste-obligations/pull/245) evidences fixture alignment, not a complete cross-repository testing architecture.
