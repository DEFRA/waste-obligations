# 0006: Derive declaration obligation coverage centrally

**Status:** Accepted—retrospective.

## Context

Coverage is a persisted value derived from declaration obligations and its early formula evolved.

## Decision

Calculate coverage through the shared calculator on submission and use the matching migration for existing documents.

## Rationale basis

**Documented:** [PR #141](https://github.com/DEFRA/waste-obligations/pull/141) requires calculation and backfill; [PR #152](https://github.com/DEFRA/waste-obligations/pull/152) aligns the retained formula/rounding. **Corroborated:** submit, calculator, and migration 004. **Inference:** one derivation aligns new and migrated values.

## Consequences and evolution

Superseded formulae are history; future definition changes assess existing values and schema compatibility.

## Evidence

- [PR #141](https://github.com/DEFRA/waste-obligations/pull/141) `b47ee410`, [PR #150](https://github.com/DEFRA/waste-obligations/pull/150) `fdb710ca`, [PR #152](https://github.com/DEFRA/waste-obligations/pull/152) `7a173b9c`.
- `src/Api/Data/Entities/ComplianceDeclaration.cs:32`, `src/Api/Data/Entities/ObligationCoveragePercentageCalculator.cs`, migration `004_ComplianceDeclarationObligationCoveragePercentage.cs`.
