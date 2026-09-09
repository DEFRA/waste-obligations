# ADR-0002: Persist compliance declarations in MongoDB

**Status:** Accepted (retrospective)
**Confidence:** High
**Source PRs:** [#12](https://github.com/DEFRA/waste-obligations/pull/12), [#18](https://github.com/DEFRA/waste-obligations/pull/18), [#20](https://github.com/DEFRA/waste-obligations/pull/20), [#25](https://github.com/DEFRA/waste-obligations/pull/25), [#35](https://github.com/DEFRA/waste-obligations/pull/35), [#39](https://github.com/DEFRA/waste-obligations/pull/39), [#56](https://github.com/DEFRA/waste-obligations/pull/56)

## Decision

Compliance declarations are persisted as MongoDB documents. Their internal identifier is a Mongo `ObjectId`, their stored timestamps are UTC, and their API timestamp representation is `DateTimeOffset`.

The identifier choice supports stable ordering for paged results. Query patterns are backed by explicit Mongo indexes rather than relying on in-memory ordering.

## Current definition

`ComplianceDeclaration.Id` is an `ObjectId`, exposed as its hexadecimal string form. `Created` and `Updated` are UTC BSON dates; values are generated without microsecond precision so they round-trip consistently through Mongo. The document has indexes/migrations for organisation/year reads and the search paths introduced later.

## Consequences

- Existing clients must treat a declaration ID as an opaque string, not a UUID.
- Stable paging includes an identifier tie-breaker where appropriate.
- Persistence-related changes are schema changes when they alter BSON or analytics payload shape; [ADR-0006](0006-version-persisted-contracts-and-migrate-forward.md) governs that evolution.

## Evidence

PR #18 added Mongo persistence following the creation endpoint scaffold. PR #20 chose `DateTimeOffset` at the API boundary while retaining UTC Mongo storage. PR #25 introduced the organisation/year index. PR #56 explicitly replaced GUIDs with `ObjectId`s so pages could be ordered stably, and PR #39 records the decision to truncate before save rather than alter values on read. The present `ComplianceDeclaration` entity, Mongo context and read queries retain those choices.
