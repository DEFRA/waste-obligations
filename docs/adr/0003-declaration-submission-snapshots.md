# 0003: Preserve selected submission-time facts

**Status:** Accepted—retrospective.

## Context

Organisation and user facts can change after a declaration is submitted.

## Decision

Persist selected submission-time organisation, registration, obligation, and user facts with the declaration.

## Rationale basis

**Documented:** #33 requires organisation data at creation; #70 identifies registration type for recipient selection/search. **Corroborated:** later changes add user name and business country. **Inference:** declarations remain interpretable without treating the snapshot as a live profile.

## Consequences and evolution

Snapshots are selective and do not replace live recipient/organisation dependencies. Shape changes use ADR 0007.

## Evidence

- #33 `d8750fbd`, #70 `90fe2d65`, #93 `c6e6b580`, #97 `989df57b`, #194 `57297f89`.
- `src/Api/Data/Entities/ComplianceDeclaration.cs`, `src/Api/Endpoints/Organisations/ComplianceDeclarations/CreateComplianceDeclaration.cs:39`.
