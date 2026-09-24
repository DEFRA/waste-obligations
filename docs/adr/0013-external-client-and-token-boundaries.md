# 0013: Separate request and background integration clients

**Status:** Accepted—retrospective.

## Context

OAuth token acquisition/proxy routing and incoming-request work have different HTTP-context needs from background work.

## Decision

Centralise OAuth/proxy configuration and the evidenced idempotent token retry. Register distinct PRN request and background clients so request correlation propagation remains on the request path.

## Rationale basis

**Documented:** [PR #180](https://github.com/DEFRA/waste-obligations/pull/180) addresses token slowness/transient failure and cancellation. **Corroborated:** named OAuth and separate PRN registrations. **Inference:** execution-context difference explains, but is not an explicit rationale for, the split.

## Consequences and evolution

Token retry does not make arbitrary integration operations retryable. Worker operation is ADRs 0014–0015.

## Evidence

- [PR #180](https://github.com/DEFRA/waste-obligations/pull/180) `33a1621`, [PR #183](https://github.com/DEFRA/waste-obligations/pull/183) `5d52d778`.
- `src/Api/Utils/OAuth2/OAuth2ServiceCollectionExtensions.cs`, `src/Api/Services/PrnCommonBackend/ServiceCollectionExtensions.cs`.
