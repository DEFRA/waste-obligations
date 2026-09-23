# 0020: Pin build inputs and separate workflow permissions

**Status:** Accepted—retrospective.

## Context

CI consumes external build inputs while testing and publishing require different privileges.

## Decision

Retain pinning of reviewed build inputs and separate read-only test jobs from publishing jobs holding repository-write/OIDC privileges.

## Rationale basis

**Documented:** #246 identifies mutable references and over-privileged test steps. **Corroborated:** Docker/workflow configuration. **Inference:** pinning constrains selection and job separation constrains capability, not trustworthiness.

## Consequences and evolution

Pins need explicit updates. Publication remains a distinct trust boundary; routine dependency bumps do not create new ADRs.

## Evidence

- #246 `0f2e418`.
- `Dockerfile`, `.github/workflows/publish.yml`, and related workflow files.
