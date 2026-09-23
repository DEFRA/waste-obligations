# 0004: Enforce lifecycle rules with optimistic concurrency

**Status:** Accepted—retrospective.

## Context

Status changes require valid transitions while concurrent requests can target one declaration.

## Decision

Own transitions and business audit in the declaration; use internal `Version` predicates for optimistic concurrency. Return 409 for conflicts and 422 for invalid transitions.

## Rationale basis

**Documented:** #59 keeps version internal; #63 distinguishes retryable conflict from invalid request. **Corroborated:** current transition methods and version-filtered updates. **Inference:** version predicates prevent lost updates.

## Consequences and evolution

#154 adds accepted-to-cancelled. Infrastructure retry cannot make an invalid transition valid; see ADR 0009.

## Evidence

- #50 `7e78ef79`, #59 `17f5e9b4`, #63 `8922782e`, #154 `197ee085`.
- `src/Api/Data/Entities/ComplianceDeclaration.cs:48,84`, `src/Api/Services/ComplianceDeclarationService.cs:244`.
