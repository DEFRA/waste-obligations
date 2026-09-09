# ADR-0003: Store submission snapshots with the declaration

**Status:** Accepted (retrospective)
**Confidence:** High
**Source PRs:** [#33](https://github.com/DEFRA/waste-obligations/pull/33), [#70](https://github.com/DEFRA/waste-obligations/pull/70), [#93](https://github.com/DEFRA/waste-obligations/pull/93), [#97](https://github.com/DEFRA/waste-obligations/pull/97)

## Decision

A compliance declaration retains the organisation and user facts submitted with it. Those facts are a snapshot of the declaration context, not a live projection of the current organisation/account record.

## Current definition

The persisted organisation contains the organisation ID and declaration-relevant values, including registration type, regulator details and the appropriate name variants. The declaration also stores a submitter name and the business audit entries retain user ID, email, name and later locale. Declaration text is deliberately not stored.

The API can still read the current organisation for route validation and notification language/recipient resolution. That does not rewrite the declaration snapshot.

## Consequences

- A declaration remains interpretable if account or organisation data subsequently changes.
- Search can filter on the declaration-time registration type and name/reference fields.
- Snapshot additions or changed semantics are persisted-contract changes and must follow ADR-0006.
- Consumers must not assume a declaration's organisation fields are a current account profile.

## Evidence

PR #33 made organisation data part of creation. PR #70 added registration type specifically for recipient selection and search, and made the organisation name optional because it is not applicable to every registration flow. PR #93 removed declaration text from the backend model. PR #97 added the user name expected from the account-facing frontend. The retained entity/DTO mapping and audit model express this history.
