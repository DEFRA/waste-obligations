# ADR-0013: Restrict operational declaration deletion by client

**Status:** Accepted (retrospective)
**Confidence:** High
**Source PR:** [#83](https://github.com/DEFRA/waste-obligations/pull/83)

## Decision

Keep the declaration deletion endpoint out of public API documentation and make it available only to explicitly allow-listed authenticated clients.

## Current definition

`DELETE /compliance-declarations/{id}` requires the write policy and `AllowedEndpointFilter`. The filter reads the authenticated client ID and permits the route only when that client's configured `AllowedEndpoints` contains the endpoint name. Any missing/unauthorised client receives `404 Not Found`; the endpoint is excluded from the OpenAPI description.

Deletion itself is transactionally concurrency-protected and emits the standard deletion analytics event described in ADR-0007.

## Consequences

- Journey/performance tooling can be granted deletion without making it a general consumer capability.
- The absence of the route from documentation is not access control; configuration and authentication are the enforcing mechanisms.
- Client allow-list changes are operational/security changes and need independent review.

## Evidence

PR #83 explicitly added deletion for performance/journey clients while keeping it disabled by default. The current endpoint metadata and `AllowedEndpointFilter` preserve that intention.
