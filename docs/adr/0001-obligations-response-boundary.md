# 0001: Keep the obligations response focused on obligations

**Status:** Accepted—retrospective.

## Context

The obligations endpoint once included organisation enrichment from Waste Organisations.

## Decision

Return an obligations-only public contract; do not restore organisation data merely because it is used elsewhere.

## Rationale basis

**Documented:** [PR #43](https://github.com/DEFRA/waste-obligations/pull/43) says the earlier shape increased coupling and required frontend remapping. **Corroborated:** current `ReadObligations` validates organisation existence then maps obligations. **Inference:** organisation presentation belongs to another boundary.

## Consequences and evolution

Internal registration naming in ADR 0012 does not establish a public organisation-name contract.

## Evidence

- [PR #3](https://github.com/DEFRA/waste-obligations/pull/3) `ef729d1`, [PR #9](https://github.com/DEFRA/waste-obligations/pull/9) `423c1b`, [PR #43](https://github.com/DEFRA/waste-obligations/pull/43) `ae8ed439`.
- `src/Api/Endpoints/Organisations/Obligations/ReadObligations.cs:26,50`.
