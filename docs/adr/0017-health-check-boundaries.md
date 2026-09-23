# 0017: Maintain lightweight and aggregate health checks

**Status:** Accepted—retrospective.

## Context

Host readiness and downstream diagnostic state are different operational signals.

## Decision

Maintain the lightweight versus aggregate health-check split, including `/health/all`, and evolve probes through their specific dependencies.

## Rationale basis

**Documented:** #37 and #52 limit diagnostic checks to aggregate health. **Corroborated:** mapping/registration and later probe updates. **Inference:** operators choose the signal appropriate to their action.

## Consequences and evolution

Health is not proof of full journey or email delivery. `/health/all` exposure rationale remains open.

## Evidence

- #1 `32a28341`, #37 `992479cf`, #52, #113, #156 `bb067e67`, #157 `c75ef725`, #172 `0242aa65`, #243.
- `src/Api/Utils/Health/WebApplicationExtensions.cs`, `ServiceCollectionExtensions.cs`.
