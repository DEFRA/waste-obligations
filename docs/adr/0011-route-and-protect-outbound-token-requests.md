# ADR-0011: Route outbound traffic and protect token requests

**Status:** Accepted (retrospective)
**Confidence:** High
**Source PRs:** [#6](https://github.com/DEFRA/waste-obligations/pull/6), [#7](https://github.com/DEFRA/waste-obligations/pull/7), [#8](https://github.com/DEFRA/waste-obligations/pull/8), [#180](https://github.com/DEFRA/waste-obligations/pull/180)

## Decision

Outbound integrations use the CDP proxy-aware HTTP handler. OAuth client-credentials token acquisition uses a shared named client with its own configured resilience pipeline, including retries for transient failures even though token acquisition is a POST.

## Current definition

`ProxyHttpMessageHandler` uses `HTTP_PROXY` when present and bypasses local traffic. Integration clients attach the cached OAuth handler and can have their own resilience configuration. The named `OAuth2Client` has total/attempt timeouts and a retry policy; it is the intentional exception to the normal no-retry-for-unsafe-methods rule because client-credentials token requests are idempotent. Cancellation is passed through to the request.

## Consequences

- Deployment networking is an application concern rather than a per-client workaround.
- Token endpoint slowness/transient failures are retried without changing the semantics of ordinary downstream writes.
- Token and resource-call behaviour remain separately configurable and observable.

## Evidence

PR #6 added proxy support after outbound traffic could not leave CDP. PRs #7 and #8 show a diagnostic removal and deliberate reintroduction of the proxy/resilience configuration. PR #180 added the dedicated token-client resilience pipeline and its cancellation/retry tests. The named client registration documents why its POST retries are allowed.
