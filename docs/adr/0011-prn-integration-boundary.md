# 0011: Adapt PRN Common Backend behind a constrained contract

**Status:** Accepted—retrospective.

## Context

PRN Common Backend vocabulary and operations require translation before becoming public API behaviour.

## Decision

Keep source translation in the local mapper/service boundary and validate public query combinations. Material filtering is only valid with `AwaitingAcceptance`; reject other combinations with 400.

## Rationale basis

**Documented:** [PR #263](https://github.com/DEFRA/waste-obligations/pull/263) identifies downstream material-filter limits and requires rejection rather than silent omission. **Corroborated:** PRN reads/searches/mappers. **Inference:** source capability does not automatically define public combinations.

## Consequences and evolution

The filter is a constrained extension of this adapter, not a separate ADR. [PR #245](https://github.com/DEFRA/waste-obligations/pull/245) supports fixture alignment only.

## Evidence

- [PR #5](https://github.com/DEFRA/waste-obligations/pull/5) `61bce9e5`, [PR #140](https://github.com/DEFRA/waste-obligations/pull/140) `2812cb16`, [PR #148](https://github.com/DEFRA/waste-obligations/pull/148) `7001cd43`, [PR #151](https://github.com/DEFRA/waste-obligations/pull/151) `d93c056d`, [PR #158](https://github.com/DEFRA/waste-obligations/pull/158) `76d2f315`, [PR #263](https://github.com/DEFRA/waste-obligations/pull/263) `4e03599`.
- `src/Api/Endpoints/Organisations/Prns/ReadPrn.cs`, `src/Api/Services/PrnCommonBackend/Mappers.cs`, `SearchOrganisationPrnsRequest.cs`, `SearchPrns.cs`.
