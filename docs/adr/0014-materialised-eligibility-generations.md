# 0014: Refresh eligibility through materialised generations

**Status:** Accepted—retrospective.

## Context

Eligibility needs source integrations while request handling needs a usable local result.

## Decision

Refresh outside requests using a leased worker, build immutable generations, and promote the active generation transactionally.

## Rationale basis

**Documented:** #183 specifies out-of-request refresh, lease coordination, generation promotion, and explicit enablement. **Corroborated:** refresh/worker services. **Inference:** promotion is the publication boundary.

## Consequences and evolution

Eligibility reflects the last promoted generation, not guaranteed live sources. Hydration/backfill has separate operational constraints.

## Evidence

- #183 `5d52d778`; supporting #184–#187, #217, #219, #229–#233, #241.
- `src/Api/Services/OrganisationEligibility/OrganisationEligibilityRefreshService.cs`, `OrganisationEligibilityRefreshWorker.cs`.
