# 0012: Select company names by source registration type

**Status:** Superseded by AMCR-506.

## Context

Waste Organisations supplies legal/trading names and a registration type; eligibility rows need one company name.

## Decision

~~Use legal `Name` for large-producer/unknown registrations, `TradingName` for a compliance scheme, with fallback
to `Name`, within the evidenced integration/eligibility scope.~~

**Superseded:** use `Name` for every registration type. The eligibility row's name is now `organisation.Name`
directly and `Organisation.CompanyName` has been removed.

## Rationale basis

**Documented:** no business explanation was found for the original rule. **Corroborated:** AMCR-506 established
that for a compliance scheme, Waste Organisations holds the scheme's own name in `Name` and the operator's legal
company name in `TradingName` — so the original selection surfaced the operator name on the regulator's
"Not submitted" tab. Every other CSoC screen already resolved to `Name`: the organisation detail page, and the
Pending/Accepted tabs via the declaration's `schemeOperatorName`.

## Consequences and evolution

The eligibility source fingerprint prefix moved to `organisation-eligibility-source-v3` so that the refresh sees
a new content fingerprint and promotes a new generation. Without that bump the source fields are unchanged, the
refresh reports `Unchanged`, and stored rows keep the old name.

Note this makes no claim about `TradingName` elsewhere. The compliance scheme's own statement view still derives
its scheme name from `TradingName`, which is tracked separately.

## Evidence

- [PR #9](https://github.com/DEFRA/waste-obligations/pull/9) `423c1b`, [PR #52](https://github.com/DEFRA/waste-obligations/pull/52) `801d9e13`, [PR #183](https://github.com/DEFRA/waste-obligations/pull/183) `5d52d778`; counter-evidence [PR #43](https://github.com/DEFRA/waste-obligations/pull/43) `ae8ed439`.
- `src/Api/Services/OrganisationEligibility/Mappers.cs:54`, `tests/Api.Tests/Services/OrganisationEligibility/MappersTests.cs`.
- AMCR-506: dev `GET /compliance-declarations/unsubmitted?registrationType=ComplianceScheme` returned operator
  names (`BARCLAYS PLC`, `AS LTD`) while the same organisations' detail pages showed the scheme name.
