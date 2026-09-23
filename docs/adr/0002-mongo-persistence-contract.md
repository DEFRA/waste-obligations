# 0002: Maintain explicit Mongo persistence conventions

**Status:** Accepted—retrospective.

## Context

Declarations expose identifiers and timestamps while MongoDB supplies their stored representation and query execution.

## Decision

Retain `ObjectId` identifiers, DateTimeOffset/UTC conversion, pre-save precision normalisation, and indexes/order aligned with Mongo query shapes.

## Rationale basis

**Documented:** #20 identifies DateTimeOffset API intent; #56 identifies stable page ordering. **Corroborated:** entity, context, indexes, and service ordering retain the conventions. **Inference:** these keep values and query execution consistent.

## Consequences and evolution

Identifier, timestamp, and query-shape changes need stored-data and public-contract review. Schema evolution is ADR 0007.

## Evidence

- #18 `738269a5`, #20 `3b276070`, #25 `ce8f8ee3`, #39 `f4f317c9`, #56 `7081b34e`, #119 `584df4b0`.
- `src/Api/Data/Entities/ComplianceDeclaration.cs`, `src/Api/Data/MongoDbContext.cs`, `src/Api/Services/ComplianceDeclarationService.cs:89`.
