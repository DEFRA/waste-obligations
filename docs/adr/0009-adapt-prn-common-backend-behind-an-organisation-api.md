# ADR-0009: Adapt PRN Common Backend behind an organisation-scoped API

**Status:** Accepted (retrospective)
**Confidence:** High
**Source PRs:** [#5](https://github.com/DEFRA/waste-obligations/pull/5), [#140](https://github.com/DEFRA/waste-obligations/pull/140), [#148](https://github.com/DEFRA/waste-obligations/pull/148), [#151](https://github.com/DEFRA/waste-obligations/pull/151), [#158](https://github.com/DEFRA/waste-obligations/pull/158)

## Decision

Expose PRN data through Waste Obligations' organisation-scoped contract while using PRN Common Backend as the operational source. Keep source response types and source-specific values behind an adapter, and validate that a returned PRN belongs to the route organisation.

## Current definition

Read and list routes first confirm the organisation exists, call PRN Common Backend with `X-EPR-ORGANISATION`, map the source response to public `Prn`/`PrnsPaged`, and return `404` when the returned recipient organisation does not match the route. Required source fields are validated during mapping; blank optional recycling process is normalised to `null`.

The organisation list has bounded page-number paging, a narrow search over PRN number/issuer name, and explicit public-to-source status/sort mappings. It makes one source search call per public page. Detail read is separate from list. A public PRN status update is deliberately singular even though the downstream API accepts a collection: the frontend determines the required individual changes and each call is an isolated downstream transaction.

## Consequences

- Public vocabulary and nullability are controlled by this API rather than source defaults.
- A source identity mismatch fails closed instead of exposing a PRN to the wrong organisation.
- New source fields, status/sort options or filters require an explicit adapter/contract mapping.

## Evidence

PR #5 introduced the PRN Common Backend integration. PR #140 established the organisation-scoped detail route and recipient verification. PR #148 documents the singular update choice. PR #151 added the paged search adapter and its extensive source-capability analysis; #158 made `recyclingProcess` optional when the source cannot reliably provide it. `AGENTS.md` changes in #140 and #151 formalise the integration-model mapping and exact public enum-validation patterns.
