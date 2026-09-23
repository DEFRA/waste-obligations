# 0014: Refresh eligibility through materialised generations

**Status:** Accepted—retrospective.

## Context

Eligibility needs source integrations while request handling needs a usable local result.

## Decision

Refresh outside requests using a leased worker, build immutable generations, and promote the active generation transactionally.

## Rationale basis

**Documented:** [PR #183](https://github.com/DEFRA/waste-obligations/pull/183) specifies out-of-request refresh, lease coordination, generation promotion, and explicit enablement. **Corroborated:** refresh/worker services. **Inference:** promotion is the publication boundary.

## Consequences and evolution

Eligibility reflects the last promoted generation, not guaranteed live sources. Hydration/backfill has separate operational constraints.

## Evidence

- [PR #183](https://github.com/DEFRA/waste-obligations/pull/183) `5d52d778`; supporting [PR #184](https://github.com/DEFRA/waste-obligations/pull/184)–[PR #187](https://github.com/DEFRA/waste-obligations/pull/187), [PR #217](https://github.com/DEFRA/waste-obligations/pull/217), [PR #219](https://github.com/DEFRA/waste-obligations/pull/219), [PR #229](https://github.com/DEFRA/waste-obligations/pull/229)–[PR #233](https://github.com/DEFRA/waste-obligations/pull/233), [PR #241](https://github.com/DEFRA/waste-obligations/pull/241).
- `src/Api/Services/OrganisationEligibility/OrganisationEligibilityRefreshService.cs`, `OrganisationEligibilityRefreshWorker.cs`.
