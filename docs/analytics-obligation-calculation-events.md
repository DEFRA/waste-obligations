# Analytics obligation calculation events

Obligation calculation changes are eligible for dispatch to a dedicated SQS queue as [analytics events](analytics-events.md), when the calculation dispatcher is enabled. That page describes the shared envelope, transport, operation values, vocabulary validation and delivery guarantees; this page describes the obligation calculation specifics.

The [calculation service](../src/Api/Services/ObligationCalculations/ObligationCalculationService.cs) records the analytics outbox event in the same Mongo transaction as the current calculation write and, for updates, the archived history write. A failed, cancelled or conflicting save leaves neither a calculation change nor an event. An equivalent recalculation, including one that differs only by calculation time or producer and material ordering, writes no new version and emits no event.

## Contract

| Field | Obligation calculation value |
| --- | --- |
| `entity` | `obligation_calculation` |
| `entityId` | `oblcalc_<calculation Mongo ObjectId>`. One calculation exists per submitter organisation and obligation year, so the ID stays stable across versions. |
| `operation` / `eventType` | `create` / `calculation.created` for the first saved result; `update` / `calculation.updated` for a materially changed result. Calculations are never deleted, so no `delete` event is published. |
| `actor` | `service:waste-obligations` |
| `correlationId` | The `x-cdp-request-id` of the admin request that started the calculation, otherwise the correlation ID generated for the scheduled run. The ID is persisted on the calculation instruction when it is created, so every event from that instruction, including retries, carries the same value. A migration gives pending or running instructions created before correlation IDs were recorded a generated ID. |
| `deletedReason` / `piiKeyRef` | Always `null`. |
| `occurredAt` | The calculation completion time (`calculatedOn`), UTC with millisecond precision. |
| `recordedAt` | When the outbox event was recorded, UTC with millisecond precision; never earlier than `occurredAt`. |
| `version` | The new calculation version: `1` for a create, incremented by one for each update. |
| `before` | `null` for a create; the exact previously stored calculation for an update. |
| `after` | The newly stored calculation. |
| `schemaVersion` | `obligation_calculation_v1.0` |

`before` and `after` are serialised against the embedded [obligation calculation v1.0 schema](../src/Api/Schemas/ObligationCalculation/obligation-calculation.v1.0.schema.json); see its [changelog](../src/Api/Schemas/ObligationCalculation/CHANGELOG.md). `GL` and `GR` carry separate obligations and repeat the same glass input weights, so those weights are not additive.

The [analytics vocabulary](../src/AuditEvents/Analytics/AnalyticsEventVocabulary.cs) registers `obligation_calculation` with the `oblcalc_` entity ID prefix, the `create` and `update` operations, both event types above and raw schema version `v1.0`.

## Delivery and consumer responsibilities

Delivery follows the shared [capture and delivery](analytics-events.md#capture-and-delivery) guarantees, so consumers must deduplicate on `eventId`. Changes for one calculation can be ordered by `entityId` and `version`.

## Rollout prerequisites

- Register `obligation_calculation`, both event types and `obligation_calculation_v1.0` in the shared governed vocabulary, and confirm the Data and Insights consumer is ready, before deploying the publisher.
- There is no backfill. A calculation saved before deployment is first published when it next materially changes, as an `update` whose `before` is the stored result; an unchanged calculation is not published.
- During a mixed-version rollout, older instances can still save calculations without emitting events. Do not treat the stream as complete across that interval.
- Reverting the publisher does not withdraw accepted queue messages. Coordinate correction or replay with the consumer if an emitted message needs remediation.

## Created event

```json
{
  "eventId": "01JZ8RXBMTY2K15SJB3PCFN3D7",
  "sequence": 125,
  "entity": "obligation_calculation",
  "entityId": "oblcalc_65f1f6570bb08052a8a27c01",
  "operation": "create",
  "eventType": "calculation.created",
  "deletedReason": null,
  "piiKeyRef": null,
  "occurredAt": "2026-01-02T03:04:05.000Z",
  "recordedAt": "2026-01-02T03:04:05.120Z",
  "actor": "service:waste-obligations",
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
  "eventType": "calculation.updated",
  "deletedReason": null,
  "piiKeyRef": null,
  "occurredAt": "2026-01-03T03:04:05.000Z",
  "recordedAt": "2026-01-03T03:04:05.090Z",
  "actor": "service:waste-obligations",
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
