# 0007: Preserve immutable versioned schemas

**Status:** Accepted—retrospective.

## Context

Persisted declarations and audit snapshots outlive the creating application version.

## Decision

Version persisted and analytics payloads, retain older embedded schemas, serialise audit events using their recorded version, and migrate live data forward without rewriting historical events.

## Rationale basis

**Documented:** schema workflow/changelog and event-flow documentation require historical compatibility. **Corroborated:** v1.0–v1.3 schema resources and version-aware serializer. **Inference:** retained schemas let newer hosts publish old events.

## Consequences and evolution

`Version` is concurrency; `SchemaVersion` is shape. Migration execution is ADR 0016 and delivery ADR 0008.

## Evidence

- [PR #99](https://github.com/DEFRA/waste-obligations/pull/99) `d01f57ca`, [PR #141](https://github.com/DEFRA/waste-obligations/pull/141) `b47ee410`, [PR #142](https://github.com/DEFRA/waste-obligations/pull/142), [PR #149](https://github.com/DEFRA/waste-obligations/pull/149), [PR #194](https://github.com/DEFRA/waste-obligations/pull/194) `57297f89`, [PR #238](https://github.com/DEFRA/waste-obligations/pull/238) `174dde3`.
- `src/Api/Schemas/ComplianceDeclaration/`, `src/Api/Schemas/EmbeddedEntityJsonSchemaProvider.cs`, `src/AuditEvents/Analytics/JsonAnalyticsEventSerializer.cs`.
