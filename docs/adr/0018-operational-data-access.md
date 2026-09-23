# 0018: Restrict destructive and diagnostic data routes

**Status:** Accepted—retrospective.

## Context

Operations need declaration deletion and persisted-data inspection, distinct from business API access.

## Decision

Restrict deletion to explicitly allowed clients and expose raw persisted-data inspection through the Admin boundary; preserve distinct privilege requirements.

## Rationale basis

**Documented:** #83 scopes deletion to test clients/environments; #226 enables lossless administrator inspection. **Corroborated:** endpoint filter and Admin group. **Inference:** one operational privilege does not establish the other.

## Consequences and evolution

Deletion is not lifecycle cancellation; raw inspection is not normal public representation.

## Evidence

- #83 `b9ff7b2`, #226 `21e70b4`.
- `src/Api/Endpoints/ComplianceDeclarations/DeleteComplianceDeclaration.cs`, `Authentication/AllowedEndpointFilter.cs`, `Endpoints/Admin/AdminEndpoints.cs`.
