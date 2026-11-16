# Analytics obligation calculation events

Obligation calculation changes are published to the shared analytics SNS topic using the generic envelope, transport and dispatch described in [Analytics compliance declaration events](analytics-compliance-declaration-events.md) and the [end-to-end event flow](compliance-declaration-end-to-end-event-flow.md).

The [calculation service](../src/Api/Services/ObligationCalculations/ObligationCalculationService.cs) records the analytics outbox event in the same Mongo transaction as the current calculation write and, for updates, the archived history write. A failed, cancelled or conflicting save leaves neither a calculation change nor an event. An equivalent recalculation, including one that differs only by calculation time or producer and material ordering, writes no new version and emits no event.

## Contract

| Field | Obligation calculation value |
| --- | --- |
| `entity` | `obligation_calculation` |
| `entityId` | `oblcalc_<calculation Mongo ObjectId>`. One calculation exists per submitter organisation and obligation year, so the ID stays stable across versions. |
| `operation` / `eventType` | `create` / `obligation_calculation.created` for the first saved result; `update` / `obligation_calculation.updated` for a materially changed result. Calculations are never deleted, so no `delete` event is published. |
| `actor` | `system:waste-obligations` |
| `correlationId` | The `x-cdp-request-id` of the admin request that started the calculation, otherwise the correlation ID generated for the scheduled run. Every event from one calculation instruction, including retries, carries the same value. An instruction persisted without a correlation ID uses its instruction ID. |
| `deletedReason` / `piiKeyRef` | Always `null`. |
| `occurredAt` | The calculation completion time (`calculatedOn`), UTC with millisecond precision. |
| `recordedAt` | When the outbox event was recorded, UTC with millisecond precision; never earlier than `occurredAt`. |
| `version` | The new calculation version: `1` for a create, incremented by one for each update. |
| `before` | `null` for a create; the exact previously stored calculation for an update. |
| `after` | The newly stored calculation. |
| `schemaVersion` | `obligation_calculation_v1.0` |

`before` and `after` are serialised against the embedded [obligation calculation v1.0 schema](../src/Api/Schemas/ObligationCalculation/obligation-calculation.v1.0.schema.json); see its [changelog](../src/Api/Schemas/ObligationCalculation/CHANGELOG.md). `GL` and `GR` carry separate obligations and repeat the same glass input weights, so those weights are not additive.

The publisher rejects an event whose entity, operation, event type, actor prefix or schema version is not registered for that entity in the [analytics vocabulary](../src/AuditEvents/Analytics/AnalyticsEventVocabulary.cs).

## Delivery and consumer responsibilities

Events use the existing outbox processor, retry, lease and dead-letter behaviour; dispatch is recorded as successful only after SNS accepts the message. Delivery is at least once, so a retried publication can repeat a message with the same `eventId`. Consumers must deduplicate on `eventId` and can order changes for one calculation by `entityId` and `version`.

## Rollout prerequisites

- Register `obligation_calculation`, both event types, `obligation_calculation_v1.0` and the `system` actor prefix in the shared governed vocabulary, and confirm the Data and Insights consumer is ready, before deploying the publisher.
- There is no backfill. A calculation saved before deployment is first published when it next materially changes, as an `update` whose `before` is the stored result; an unchanged calculation is not published.
- During a mixed-version rollout, older instances can still save calculations without emitting events. Do not treat the stream as complete across that interval.
- SNS cannot retract an accepted message, and reverting the publisher does not withdraw published events. Coordinate correction or replay with the consumer if an emitted message needs remediation.

## Created event

```json
{
  "eventId": "01JZ8RXBMTY2K15SJB3PCFN3D7",
  "sequence": 125,
  "entity": "obligation_calculation",
  "entityId": "oblcalc_65f1f6570bb08052a8a27c01",
  "operation": "create",
  "eventType": "obligation_calculation.created",
  "deletedReason": null,
  "piiKeyRef": null,
  "occurredAt": "2026-01-02T03:04:05.000Z",
  "recordedAt": "2026-01-02T03:04:05.120Z",
  "actor": "system:waste-obligations",
  "correlationId": "cdp-request-id",
  "version": 1,
  "before": null,
  "after": {
    "id": "65f1f6570bb08052a8a27c01",
    "version": 1,
    "schemaVersion": "v1.0",
    "submitterOrganisationId": "8b0f0d4e-3c1a-4a51-9f0e-2d7c5b6a1e01",
    "submitterType": "ComplianceScheme",
    "obligationYear": 2026,
    "calculatedOn": "2026-01-02T03:04:05+00:00",
    "producers": [
      {
        "producerOrganisationId": "c2a4e6f8-1b3d-4f5a-8c7e-9d0b1a2c3e02",
        "materials": [
          { "materialCode": "GL", "inputKilograms": 3000, "inputTonnes": 3, "obligationTonnes": 1 },
          { "materialCode": "GR", "inputKilograms": 3000, "inputTonnes": 3, "obligationTonnes": 2 }
        ]
      }
    ]
  },
  "schemaVersion": "obligation_calculation_v1.0"
}
```

## Updated event

```json
{
  "eventId": "01JZ8RXBMTY2K15SJB3PCFN3D8",
  "sequence": 126,
  "entity": "obligation_calculation",
  "entityId": "oblcalc_65f1f6570bb08052a8a27c01",
  "operation": "update",
  "eventType": "obligation_calculation.updated",
  "deletedReason": null,
  "piiKeyRef": null,
  "occurredAt": "2026-01-03T03:04:05.000Z",
  "recordedAt": "2026-01-03T03:04:05.090Z",
  "actor": "system:waste-obligations",
  "correlationId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "version": 2,
  "before": {
    "id": "65f1f6570bb08052a8a27c01",
    "version": 1,
    "schemaVersion": "v1.0",
    "submitterOrganisationId": "8b0f0d4e-3c1a-4a51-9f0e-2d7c5b6a1e01",
    "submitterType": "ComplianceScheme",
    "obligationYear": 2026,
    "calculatedOn": "2026-01-02T03:04:05+00:00",
    "producers": [
      {
        "producerOrganisationId": "c2a4e6f8-1b3d-4f5a-8c7e-9d0b1a2c3e02",
        "materials": [
          { "materialCode": "GL", "inputKilograms": 3000, "inputTonnes": 3, "obligationTonnes": 1 },
          { "materialCode": "GR", "inputKilograms": 3000, "inputTonnes": 3, "obligationTonnes": 2 }
        ]
      }
    ]
  },
  "after": {
    "id": "65f1f6570bb08052a8a27c01",
    "version": 2,
    "schemaVersion": "v1.0",
    "submitterOrganisationId": "8b0f0d4e-3c1a-4a51-9f0e-2d7c5b6a1e01",
    "submitterType": "ComplianceScheme",
    "obligationYear": 2026,
    "calculatedOn": "2026-01-03T03:04:05+00:00",
    "producers": [
      {
        "producerOrganisationId": "c2a4e6f8-1b3d-4f5a-8c7e-9d0b1a2c3e02",
        "materials": [
          { "materialCode": "GL", "inputKilograms": 4000, "inputTonnes": 4, "obligationTonnes": 3 },
          { "materialCode": "GR", "inputKilograms": 4000, "inputTonnes": 4, "obligationTonnes": 3 }
        ]
      }
    ]
  },
  "schemaVersion": "obligation_calculation_v1.0"
}
```
