# 0019: Validate route and body identity before persistence

**Status:** Accepted—retrospective.

## Context

Creation contains organisation identity in both route and body; a mismatch could persist inconsistent declaration/audit data.

## Decision

Validate the identifiers before persistence; return 400 for mismatch and retain 404 for an unknown route organisation.

## Rationale basis

**Documented:** [PR #239](https://github.com/DEFRA/waste-obligations/pull/239) identifies the persisted/audit mismatch risk. **Corroborated:** validation precedes create. **Inference:** distinct errors preserve invalid-request versus unknown-resource meaning.

## Consequences and evolution

This is separate from lifecycle, retry, and snapshot rules.

## Evidence

- [PR #239](https://github.com/DEFRA/waste-obligations/pull/239) `fca6f44`.
- `src/Api/Endpoints/Organisations/ComplianceDeclarations/CreateComplianceDeclaration.cs:39`.
