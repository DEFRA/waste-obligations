# 0019: Validate route and body identity before persistence

**Status:** Accepted—retrospective.

## Context

Creation contains organisation identity in both route and body; a mismatch could persist inconsistent declaration/audit data.

## Decision

Validate the identifiers before persistence; return 400 for mismatch and retain 404 for an unknown route organisation.

## Rationale basis

**Corroborated:** [PR #239](https://github.com/DEFRA/waste-obligations/pull/239) adds validation before create. **Inference:** mismatched IDs could make persisted declaration and audit identity inconsistent, while distinct errors preserve invalid-request versus unknown-resource meaning.

## Consequences and evolution

This equality check does not define what an organisation ID denotes across service boundaries; ADR 0021 records that semantic contract. It remains separate from lifecycle, retry, and snapshot rules.

## Evidence

- [PR #239](https://github.com/DEFRA/waste-obligations/pull/239) `fca6f44`.
- `src/Api/Endpoints/Organisations/ComplianceDeclarations/CreateComplianceDeclaration.cs:39`.
