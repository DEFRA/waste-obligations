# 0010: Keep Notify delivery outside declaration success

**Status:** Accepted—retrospective.

## Context

Declaration mutations can trigger email but delivery and recipient resolution use external services.

## Decision

Treat Notify delivery as best effort; notification failure does not roll back or fail a successful declaration change.

## Rationale basis

**Documented:** #159 and #178 preserve cancellation when account/notification parameters fail. **Corroborated:** endpoint ordering and EmailService failure handling. **Inference:** successful state change does not prove delivery.

## Consequences and evolution

Recipients use live workflow data; snapshots do not replace it. Template/recipient repairs evolve this workflow, not a second delivery architecture.

## Evidence

- #52 `801d9e13`, #98 `15ab20df`, #108 `f9c50c96`, #159 `13832d26`, #178 `bb23a0c5`, #179 `45e58665`.
- `src/Api/Services/EmailService.cs`, create/update compliance-declaration endpoints.
