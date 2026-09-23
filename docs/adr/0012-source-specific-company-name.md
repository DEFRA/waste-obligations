# 0012: Select company names by source registration type

**Status:** Accepted—retrospective.

## Context

Waste Organisations supplies legal/trading names and a registration type; eligibility rows need one company name.

## Decision

Use legal `Name` for large-producer/unknown registrations, `TradingName` for a compliance scheme, with fallback to `Name`, within the evidenced integration/eligibility scope.

## Rationale basis

**Documented:** no business explanation was found. **Corroborated:** selector, projection mapper, and focused tests retain the rule. **Inference:** changing it changes materialised eligibility data, so it is a durable source-domain policy.

## Consequences and evolution

This is not a universal display-name policy. #43 limits any inference about the obligations response.

## Evidence

- #9 `423c1b`, #52 `801d9e13`, #183 `5d52d778`; counter-evidence #43 `ae8ed439`.
- `src/Api/Services/WasteOrganisations/Organisation.cs:33`, `src/Api/Services/OrganisationEligibility/Mappers.cs:27,54`, `tests/Api.Tests/Services/WasteOrganisations/OrganisationTests.cs`.
