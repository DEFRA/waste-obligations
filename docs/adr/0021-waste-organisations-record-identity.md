# 0021: Preserve Waste Organisations record identity across declarations and eligibility projections

**Status:** Accepted—retrospective.

## Context

`organisationId` is used by organisation-scoped API routes, the submitted compliance-declaration snapshot, the unsubmitted eligibility projection, background obligation hydration, Account reference resolution, and PRN Common Backend requests. Those uses have different storage and transport forms but need to refer to the same business record.

A direct producer and a compliance scheme are different kinds of Waste Organisations record. In particular, a compliance-scheme ID must not be silently replaced with its operator organisation's Account identifier.

## Decision

Treat an `organisationId` accepted by this service as the identifier of the corresponding Waste Organisations record. Preserve that value without local translation when it is stored in a declaration, materialised into eligibility and obligation-summary rows, or sent to the PRN Common Backend.

For unsubmitted eligibility, the record ID alone is not the business key: use `(organisationId, obligationYear, registrationType)`. Do not describe this ID as an Account organisation ID or as a declaration ID.

## Rationale basis

**Documented (external):** the [frontend ADR 0002](https://github.com/DEFRA/waste-obligations-frontend/blob/main/docs/adr/0002-actor-specific-path-identifier-contract.md) records the complementary producer/CSO path contract: a producer path identifies a direct-producer record and a CSO path normally identifies a compliance-scheme record, not its operator. That ADR is supporting evidence only: it explicitly leaves RPD/Common Data identifier provenance unverified.

**Corroborated:** [PR #239](https://github.com/DEFRA/waste-obligations/pull/239) adds route/body equality validation before persistence. The create endpoint reads the route value from Waste Organisations and requires the body value to match. Eligibility refresh maps the source `Organisation.Id` directly to `OrganisationId`; hydration and PRN requests then reuse it. Direct-producer reference resolution matches that value to Account `ExternalId`, while compliance-scheme resolution uses Companies House number instead.

**Inference:** retaining one source-record identifier makes submitted declarations, unsubmitted eligibility, and background obligation metrics joinable without treating an operator association as the scheme identity; mismatched route/body IDs would otherwise make persisted declaration identity inconsistent.

## Consequences and evolution

The declaration stores a submission-time organisation snapshot whose `Id` is the route/source-record ID. The unsubmitted view suppresses a row only when a submitted or accepted declaration has the same ID, obligation year, and registration type. Changing the meaning or representation of this identifier would therefore require a cross-service contract change and a plan for persisted declarations, eligibility generations, and obligation summaries.

The create endpoint establishes only that the route record exists and that route/body IDs agree. It does not verify that the submitted registration type belongs to that Waste Organisations record. The system consequently relies on its caller to keep the identifier and registration type aligned; an incorrect type could affect which unsubmitted row is suppressed. This is a confidence gap, not evidence of an existing production mismatch.

This record complements ADR 0019's route/body equality check, ADR 0003's selective declaration snapshot, and ADR 0014's generation lifecycle. Those records do not define the cross-service meaning of the ID.

## Evidence

- [PR #9](https://github.com/DEFRA/waste-obligations/pull/9) `423c1b8`, [PR #33](https://github.com/DEFRA/waste-obligations/pull/33) `d8750fbd`, [PR #183](https://github.com/DEFRA/waste-obligations/pull/183) `5d52d778`, [PR #185](https://github.com/DEFRA/waste-obligations/pull/185) `3839fe6`, and [PR #239](https://github.com/DEFRA/waste-obligations/pull/239) `fca6f44`; supporting external evidence: [frontend ADR 0002](https://github.com/DEFRA/waste-obligations-frontend/blob/main/docs/adr/0002-actor-specific-path-identifier-contract.md).
- `src/Api/Endpoints/Organisations/ComplianceDeclarations/CreateComplianceDeclaration.cs:40`, `src/Api/Dtos/Mappers.cs:56`, `src/Api/Services/OrganisationEligibility/Mappers.cs:49`, `src/Api/Services/UnsubmittedEligibilityVisibilityService.cs:58`, `src/Api/Services/OrganisationObligations/OrganisationObligationHydrationService.cs:470`, `src/Api/Services/PrnCommonBackend/PrnCommonBackendService.cs:15`, and `src/Api/Services/OrganisationEligibility/OrganisationReferenceResolver.cs:100`.

**Confidence:** High for the retained API, persistence, and background-processing behaviour; medium for the producer/CSO cross-repository path contract; low for the uninspected RPD/Common Data provenance.
