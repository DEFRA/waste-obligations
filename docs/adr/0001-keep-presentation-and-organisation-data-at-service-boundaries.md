# ADR-0001: Keep presentation and organisation data at service boundaries

**Status:** Accepted (retrospective)
**Confidence:** High
**Source PRs:** [#3](https://github.com/DEFRA/waste-obligations/pull/3), [#9](https://github.com/DEFRA/waste-obligations/pull/9), [#43](https://github.com/DEFRA/waste-obligations/pull/43)

## Decision

The obligations API owns an obligations contract, not an organisation or presentation contract. It may use Waste Organisations to validate an organisation exists, and PRN Common Backend to obtain obligation data, but returns only obligations in `GET /organisations/{organisationId}/obligations`.

Translation and presentation decisions remain with the frontend. Integration-specific models are adapted to public DTOs at the endpoint/service boundary rather than being returned directly.

## Current definition

The organisation ID in the route scopes the request; it is not a request to embed an organisation view. The API maps PRN Common Backend fields into its own obligation DTO, including normalising missing downstream tonnages to zero where the public contract requires a non-null number.

## Consequences

- The API avoids duplicating frontend business-country/presentation logic and avoids coupling its response to the Waste Organisations shape.
- Consumers needing organisation detail obtain or derive it through the appropriate organisation boundary.
- Downstream model changes require an adapter review rather than silently changing the public contract.

## Evidence

PR #3 introduced an optional organisation inclusion. PR #9 introduced Waste Organisations enrichment. PR #43 explicitly removed organisation data from the obligations response because it increased service coupling and returned presentation text that the frontend would need to remap. `ReadObligations` now uses Waste Organisations only to decide whether to return `404`, then maps only the obligations collection.

`AGENTS.md`, added later, codifies the same boundary rule: integration clients return integration response models and the consuming endpoint or service maps them to public DTOs. See [ADR-0009](0009-adapt-prn-common-backend-behind-an-organisation-api.md) for its PRN-specific application.
