# 0004: Enforce lifecycle rules with optimistic concurrency

**Status:** Accepted—retrospective.

## Context

Status changes require valid transitions while concurrent requests can target one declaration.

## Decision

Own transitions and business audit in the declaration; use internal `Version` predicates for optimistic concurrency. Return 409 for conflicts and 422 for invalid transitions.

## Rationale basis

**Documented:** [PR #59](https://github.com/DEFRA/waste-obligations/pull/59) keeps version internal; [PR #63](https://github.com/DEFRA/waste-obligations/pull/63) distinguishes retryable conflict from invalid request. **Corroborated:** current transition methods and version-filtered updates. **Inference:** version predicates prevent lost updates.

## Consequences and evolution

[PR #154](https://github.com/DEFRA/waste-obligations/pull/154) adds accepted-to-cancelled. Infrastructure retry cannot make an invalid transition valid; see ADR 0009.

## Evidence

- [PR #50](https://github.com/DEFRA/waste-obligations/pull/50) `7e78ef79`, [PR #59](https://github.com/DEFRA/waste-obligations/pull/59) `17f5e9b4`, [PR #63](https://github.com/DEFRA/waste-obligations/pull/63) `8922782e`, [PR #154](https://github.com/DEFRA/waste-obligations/pull/154) `197ee085`.
- `src/Api/Data/Entities/ComplianceDeclaration.cs:48,84`, `src/Api/Services/ComplianceDeclarationService.cs:244`.
