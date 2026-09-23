# 0008: Deliver analytics through a transactional outbox

**Status:** Accepted—retrospective.

## Context

A declaration mutation and its analytics publication have different reliability boundaries.

## Decision

Write the audit event in the declaration transaction, then dispatch persisted events asynchronously through leased processing.

## Rationale basis

**Documented:** PR #99 establishes the event/dispatch path. **Corroborated:** transactional recording, dispatch states, leasing, and processor remain. **Inference:** recording survives a delivery failure for later work.

## Consequences and evolution

Delivery is at-least-once; consumers deduplicate. Dead-letter is persisted state, with no evidenced remediation process. #238 changes envelope compatibility, not the boundary.

## Evidence

- #99 `d01f57ca`, #238 `174dde3`.
- `src/AuditEvents/AuditEventService.cs`, `AuditEventDispatchService.cs`, `AuditEventLeaseService.cs`, `Analytics/AnalyticsAuditEventProcessor.cs`.
