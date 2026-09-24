# 0009: Centralise selected Mongo transaction retries

**Status:** Accepted—retrospective.

## Context

Transient transaction/write-conflict errors occur after a caller has entered the database boundary.

## Decision

Own sessions, transaction options, bounded execution, and selected retry classification in `MongoDbContext`; retry `TransientTransactionError` and supported code-112 failures with jitter.

## Rationale basis

**Documented:** [PR #160](https://github.com/DEFRA/waste-obligations/pull/160) says callers cannot safely retry; [PR #161](https://github.com/DEFRA/waste-obligations/pull/161) centralises for reuse. **Corroborated:** declaration and eligibility operations use the shared boundary. **Inference:** callbacks must be safe to re-execute.

## Consequences and evolution

This is not general retry. Lifecycle version guards remain ADR 0004; hydration/backfill has a distinct nontransactional model.

## Evidence

- [PR #99](https://github.com/DEFRA/waste-obligations/pull/99) `d01f57ca`, [PR #160](https://github.com/DEFRA/waste-obligations/pull/160) `1f493947`, [PR #161](https://github.com/DEFRA/waste-obligations/pull/161) `838c2f6d`.
- `src/Api/Data/MongoDbContext.cs`, `src/Api/Services/ComplianceDeclarationService.cs`, `OrganisationEligibilityRefreshService.cs`.
