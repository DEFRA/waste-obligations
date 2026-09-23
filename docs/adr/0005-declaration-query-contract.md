# 0005: Constrain declaration search, sorting and paging

**Status:** Accepted—retrospective.

## Context

Declaration search maps public input into Mongo queries.

## Decision

Accept only the validated search/sort/paging grammar; escape text search and execute sort/paging in Mongo against supported index shapes.

## Rationale basis

**Documented:** #119 requires database paging at volume; #171 requires escaped, scoped search. **Corroborated:** request validation and query code. **Inference:** a constrained grammar keeps execution assessable.

## Consequences and evolution

New filters require validation, execution, and index review. Unsubmitted search is a separate materialised path.

## Evidence

- #65 `a6fbe3eb`, #68 `b2f203b7`, #119 `584df4b0`, #170 `556f4fd8`, #171 `66a233e4`.
- `src/Api/Dtos/SearchComplianceDeclarationsRequest.cs`, `src/Api/Services/ComplianceDeclarationService.cs:186`.
