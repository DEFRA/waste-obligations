# 0017: Maintain lightweight and aggregate health checks

**Status:** Accepted—retrospective.

## Context

Host readiness and downstream diagnostic state are different operational signals.

## Decision

Maintain the lightweight versus aggregate health-check split, including `/health/all`, and evolve probes through their specific dependencies.

## Rationale basis

**Documented:** [PR #37](https://github.com/DEFRA/waste-obligations/pull/37) and [PR #52](https://github.com/DEFRA/waste-obligations/pull/52) limit diagnostic checks to aggregate health. **Corroborated:** mapping/registration and later probe updates. **Inference:** operators choose the signal appropriate to their action.

## Consequences and evolution

Health is not proof of full journey or email delivery. `/health/all` exposure rationale remains open.

## Evidence

- [PR #1](https://github.com/DEFRA/waste-obligations/pull/1) `32a28341`, [PR #37](https://github.com/DEFRA/waste-obligations/pull/37) `992479cf`, [PR #52](https://github.com/DEFRA/waste-obligations/pull/52), [PR #113](https://github.com/DEFRA/waste-obligations/pull/113), [PR #156](https://github.com/DEFRA/waste-obligations/pull/156) `bb067e67`, [PR #157](https://github.com/DEFRA/waste-obligations/pull/157) `c75ef725`, [PR #172](https://github.com/DEFRA/waste-obligations/pull/172) `0242aa65`, [PR #243](https://github.com/DEFRA/waste-obligations/pull/243).
- `src/Api/Utils/Health/WebApplicationExtensions.cs`, `ServiceCollectionExtensions.cs`.
