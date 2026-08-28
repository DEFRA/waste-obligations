# ADR-0008: Send best-effort, recipient-specific notifications

**Status:** Accepted (retrospective)
**Confidence:** High
**Source PRs:** [#52](https://github.com/DEFRA/waste-obligations/pull/52), [#98](https://github.com/DEFRA/waste-obligations/pull/98), [#108](https://github.com/DEFRA/waste-obligations/pull/108), [#112](https://github.com/DEFRA/waste-obligations/pull/112), [#144](https://github.com/DEFRA/waste-obligations/pull/144), [#155](https://github.com/DEFRA/waste-obligations/pull/155), [#159](https://github.com/DEFRA/waste-obligations/pull/159), [#178](https://github.com/DEFRA/waste-obligations/pull/178), [#179](https://github.com/DEFRA/waste-obligations/pull/179)

## Decision

Use Gov.uk Notify for submission and cancellation notifications, after the declaration mutation has committed. Notification delivery is best effort: a notification failure is logged and measured but does not undo or fail the declaration state change.

## Current definition

Submission sends one message to the user recorded in the declaration's `Submitted` audit entry. The selected template depends on declaration registration type; language derives from the current organisation business country. Regulator leading/inline values are prepared as notification personalisation rather than being general presentation data.

Cancellation sends only for a recognised cancellation reason. Recipients are resolved from Account Backend as the original submitter (when matched to a complete person record) and the organisation's `Approved Person`; duplicate addresses are removed and the output is deterministically ordered. Missing notification parameters do not block cancellation. No resolvable recipient means no cancellation message, not a failed cancellation.

## Consequences

- Declaration persistence and email delivery have separate reliability semantics.
- Notification recipient data is current account data, while the declaration remains an immutable submission snapshot.
- Template, localisation and personalisation changes need end-to-end Notify coverage.

## Evidence

PR #52 introduced Notify submission messages; #98 deliberately narrowed their recipient from all organisation users to the actual submitter. PR #108 selected a compliance-scheme variant and language from business country. Account Backend was removed in #155 when no longer needed, then reintroduced for cancellation in #159; PR #179 corrected the cancellation rule to submitter plus Approved Person. PR #178 removed notification-parameter validation as a precondition for cancellation. The current `EmailService` and `CancellationEmailRecipientResolver` encode these definitions.
