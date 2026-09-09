# ADR-0006: Version persisted contracts and migrate forward

**Status:** Accepted (retrospective)
**Confidence:** High
**Source PRs:** [#102](https://github.com/DEFRA/waste-obligations/pull/102), [#134](https://github.com/DEFRA/waste-obligations/pull/134), [#141](https://github.com/DEFRA/waste-obligations/pull/141), [#142](https://github.com/DEFRA/waste-obligations/pull/142), [#149](https://github.com/DEFRA/waste-obligations/pull/149), [#150](https://github.com/DEFRA/waste-obligations/pull/150), [#152](https://github.com/DEFRA/waste-obligations/pull/152)

## Decision

The compliance declaration's persisted BSON and analytics payload are versioned contracts. Each published JSON schema is immutable. Shape changes create a new major/minor schema version and, where necessary, a new idempotent Mongo migration; they do not rewrite historical audit-event snapshots.

## Current definition

`Version` is an optimistic-concurrency counter. `SchemaVersion` identifies the BSON/analytics shape and is currently `v1.2`. Version `v1.0`, `v1.1` and `v1.2` schema files remain embedded. A stored audit event records the schema version that was current for its immutable `before`/`after` snapshot, allowing later hosts to serialise undispatched historic events with the correct schema.

Compatible additions use a minor version; breaking representation/semantic changes require a major version or an expand/backfill/contract rollout. Migrations act on raw BSON, target precise source versions, preserve already-migrated values, and advance data and schema version together.

## Consequences

- A declaration/entity change is assessed across persistence, DTOs, schemas, audit snapshots, analytics, fixtures, migrations and documentation.
- Historic audit events are never backfilled merely to make them look current.
- The schema changelog is part of the contract record.

## Evidence

PR #102 established embedded schema resolution and schema-aware analytics serialisation. PR #134 added locale as `v1.1`; PR #141 added the optional coverage percentage as `v1.2`. PR #142 introduced the explicit schema-change workflow, and PR #149 added the required changelog step. `AGENTS.md` is unusually strong corroboration here: it specifies version classification, immutable schema files, raw-BSON idempotent migrations, retained historical audit events, and compatibility testing.
