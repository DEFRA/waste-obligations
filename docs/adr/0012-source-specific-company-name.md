# 0012: Select company names by source registration type

**Status:** Accepted—retrospective.

## Context

Waste Organisations supplies legal/trading names and a registration type; eligibility rows need one company name.

## Decision

Use legal `Name` for large-producer/unknown registrations, `TradingName` for a compliance scheme, with fallback to `Name`, within the evidenced integration/eligibility scope.

## Rationale basis

**Documented:** no business explanation was found. **Corroborated:** selector, projection mapper, and focused tests retain the rule. **Inference:** changing it changes materialised eligibility data, so it is a durable source-domain policy.

## Consequences and evolution

This is not a universal display-name policy. [PR #43](https://github.com/DEFRA/waste-obligations/pull/43) limits any inference about the obligations response.

**Amended (AMCR-506):** the fallback to `Name` now fires for a blank `TradingName` as well as a null one. Waste Organisations applies no validation to `tradingName` and accepts an empty string, so a null-only fallback materialised an empty eligibility `Name`. That is both the displayed organisation name and the sort and search key for the unsubmitted endpoint, so an empty value rendered an empty cell in the regulator's not-submitted list and sorted ahead of every real name. The selection rule itself is unchanged. Because the refresh fingerprint hashes the raw source fields rather than the derived name, this change required bumping the fingerprint seed to `organisation-eligibility-source-v3` to force re-materialisation under ADR 0014's generation lifecycle.

## Evidence

- [PR #9](https://github.com/DEFRA/waste-obligations/pull/9) `423c1b`, [PR #52](https://github.com/DEFRA/waste-obligations/pull/52) `801d9e13`, [PR #183](https://github.com/DEFRA/waste-obligations/pull/183) `5d52d778`; counter-evidence [PR #43](https://github.com/DEFRA/waste-obligations/pull/43) `ae8ed439`.
- `src/Api/Services/WasteOrganisations/Organisation.cs:33`, `src/Api/Services/OrganisationEligibility/Mappers.cs:27,54`, `tests/Api.Tests/Services/WasteOrganisations/OrganisationTests.cs`.
