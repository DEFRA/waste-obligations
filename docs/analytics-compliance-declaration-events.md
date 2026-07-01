# Analytics compliance declaration events

The analytics topic receives generic analytics event envelopes. Each envelope identifies the entity, operation, event type, entity version, and the versioned payload schema. Compliance declarations are the first entity type published by this service; future entity types should use the same envelope.

Compliance declaration create, update, and delete operations are captured internally in the same transaction as the declaration change. The analytics processor reads undispatched changes, serialises them as analytics events, and publishes them to the analytics SNS topic configured by `AnalyticsAuditEventProcessor:TopicArn`.

The nested compliance declaration payload is serialised using the embedded [compliance declaration schema](../src/Api/Schemas/ComplianceDeclaration/compliance-declaration.v1.0.schema.json). For compliance declarations, the analytics message `schemaVersion` is currently `compliance_declaration.v1.0`.

## Message transport

Messages are published with the SNS message attribute `Content-Type` set to `application/json`.

If the JSON body is too large for the SNS message size budget, the body is gzip-compressed and base64-encoded. Compressed messages include the SNS message attribute `Content-Encoding` set to `gzip+base64`.

## Analytics event envelope

All analytics events use this generic envelope:

```json
{
  "eventId": "01JZ8RXBMTY2K15SJB3PCFN3D5",
  "sequence": 123,
  "entity": "entity_name",
  "entityId": "entity_name_entity-id",
  "operation": "insert",
  "eventType": "domain.event",
  "deletedReason": null,
  "piiKeyRef": null,
  "occurredAt": "2026-01-02T03:04:05+00:00",
  "recordedAt": "2026-01-02T03:04:06+00:00",
  "actor": "service:waste-obligations",
  "version": 1,
  "before": null,
  "after": {},
  "schemaVersion": "entity_name.v1.0"
}
```

| Field | Description |
| --- | --- |
| `eventId` | ULID generated for the analytics event. |
| `sequence` | Global analytics event sequence allocated when the event is written. |
| `entity` | Logical entity type for the payload. |
| `entityId` | Unique identifier for the entity instance as emitted by the entity mapper. |
| `operation` | Event operation. See the operation values below. |
| `eventType` | Business event name. |
| `deletedReason` | Reason the entity was deleted. This is only set when `operation` is `delete`; otherwise it is `null`. |
| `piiKeyRef` | Currently always `null`. |
| `occurredAt` | Time the entity change occurred, in ISO 8601 format with offset. |
| `recordedAt` | Time the analytics event was recorded, in ISO 8601 format with offset. |
| `actor` | Service actor that wrote the event. |
| `version` | Entity version after the operation. |
| `before` | Previous entity state. This is `null` for inserts. |
| `after` | New entity state. This is `null` for deletes. |
| `schemaVersion` | Entity-qualified schema version for the `before` and `after` payloads. |

## Operation values

| Operation | Meaning | `before` | `after` | `deletedReason` |
| --- | --- | --- | --- | --- |
| `insert` | Entity was created. | `null` | Created entity state. | `null` |
| `update` | Entity was changed. | Previous entity state. | Updated entity state. | `null` |
| `delete` | Entity was deleted. | Previous entity state. | `null` | Delete reason. |

## Compliance declaration events

Compliance declaration events use `entity` set to `compliance_declaration`. The `entityId` value is prefixed with the entity type, for example `compliance_declaration_65f1f6570bb08052a8a27b01`.

The current compliance declaration event types are:

| Event type | Operation | Description |
| --- | --- | --- |
| `submission.created` | `insert` | Compliance declaration was created. |
| `submission.amended` | `update` | Compliance declaration was updated. |
| `submission.removed` | `delete` | Compliance declaration was deleted. |

## Created event

When a compliance declaration is created, the analytics topic receives an `insert` event with `eventType` set to `submission.created`.

The `before` value is `null`. The `after` value is the created compliance declaration, serialised according to the linked compliance declaration schema.

```json
{
  "eventId": "01JZ8RXBMTY2K15SJB3PCFN3D5",
  "sequence": 123,
  "entity": "compliance_declaration",
  "entityId": "compliance_declaration_65f1f6570bb08052a8a27b01",
  "operation": "insert",
  "eventType": "submission.created",
  "deletedReason": null,
  "piiKeyRef": null,
  "occurredAt": "2026-01-02T03:04:05+00:00",
  "recordedAt": "2026-01-02T03:04:06+00:00",
  "actor": "service:waste-obligations",
  "version": 1,
  "before": null,
  "after": {
    "id": "65f1f6570bb08052a8a27b01",
    "version": 1,
    "created": "2026-01-02T03:04:05+00:00",
    "updated": "2026-01-02T03:04:05+00:00",
    "status": "Submitted",
    "organisation": {
      "id": "5dbef606-3611-42f4-b39f-cad828badc12",
      "registrationType": "DirectProducer",
      "name": "Org Name",
      "complianceSchemeName": null,
      "schemeOperatorName": null,
      "referenceNumber": "123456",
      "address": {
        "addressLine1": "Test Name Ltd",
        "addressLine2": "123 Street",
        "town": "Town",
        "county": "County",
        "postcode": "UK1",
        "country": "UK"
      },
      "regulator": "Regulator",
      "regulatorEmail": "regulator@email.com"
    },
    "obligationYear": 2026,
    "obligations": [
      {
        "material": "Plastic",
        "recyclingTarget": 0.75,
        "tonnages": {
          "material": 100,
          "awaitingAcceptance": 10,
          "accepted": 2,
          "outstanding": 20,
          "obligated": 5
        },
        "status": "NoDataYet"
      }
    ],
    "obligationStatus": "NotMet",
    "submitterName": "Submitter Name",
    "audit": [
      {
        "action": "Submitted",
        "user": {
          "id": "e72be574-8b5b-4836-af47-dd7e0c0d1d87",
          "email": "submitter@email.com",
          "name": "Submitter Name"
        },
        "timestamp": "2026-01-02T03:04:05+00:00"
      }
    ],
    "isRegulation43Compliant": true
  },
  "schemaVersion": "compliance_declaration.v1.0"
}
```

## Updated event

When a compliance declaration is updated, the analytics topic receives an `update` event with `eventType` set to `submission.amended`.

The `before` value is the declaration state before the update. The `after` value is the declaration state after the update. Both payloads use the same compliance declaration schema.

```json
{
  "eventId": "01JZ8RXBMTY2K15SJB3PCFN3D6",
  "sequence": 124,
  "entity": "compliance_declaration",
  "entityId": "compliance_declaration_65f1f6570bb08052a8a27b01",
  "operation": "update",
  "eventType": "submission.amended",
  "deletedReason": null,
  "piiKeyRef": null,
  "occurredAt": "2026-01-02T03:05:05+00:00",
  "recordedAt": "2026-01-02T03:05:06+00:00",
  "actor": "service:waste-obligations",
  "version": 2,
  "before": {
    "id": "65f1f6570bb08052a8a27b01",
    "version": 1,
    "created": "2026-01-02T03:04:05+00:00",
    "updated": "2026-01-02T03:04:05+00:00",
    "status": "Submitted",
    "organisation": {
      "id": "5dbef606-3611-42f4-b39f-cad828badc12",
      "registrationType": "DirectProducer",
      "name": "Org Name",
      "complianceSchemeName": null,
      "schemeOperatorName": null,
      "referenceNumber": "123456",
      "address": {
        "addressLine1": "Test Name Ltd",
        "addressLine2": "123 Street",
        "town": "Town",
        "county": "County",
        "postcode": "UK1",
        "country": "UK"
      },
      "regulator": "Regulator",
      "regulatorEmail": "regulator@email.com"
    },
    "obligationYear": 2026,
    "obligations": [
      {
        "material": "Plastic",
        "recyclingTarget": 0.75,
        "tonnages": {
          "material": 100,
          "awaitingAcceptance": 10,
          "accepted": 2,
          "outstanding": 20,
          "obligated": 5
        },
        "status": "NoDataYet"
      }
    ],
    "obligationStatus": "NotMet",
    "submitterName": "Submitter Name",
    "audit": [
      {
        "action": "Submitted",
        "user": {
          "id": "e72be574-8b5b-4836-af47-dd7e0c0d1d87",
          "email": "submitter@email.com",
          "name": "Submitter Name"
        },
        "timestamp": "2026-01-02T03:04:05+00:00"
      }
    ],
    "isRegulation43Compliant": true
  },
  "after": {
    "id": "65f1f6570bb08052a8a27b01",
    "version": 2,
    "created": "2026-01-02T03:04:05+00:00",
    "updated": "2026-01-02T03:05:05+00:00",
    "status": "Accepted",
    "organisation": {
      "id": "5dbef606-3611-42f4-b39f-cad828badc12",
      "registrationType": "DirectProducer",
      "name": "Org Name",
      "complianceSchemeName": null,
      "schemeOperatorName": null,
      "referenceNumber": "123456",
      "address": {
        "addressLine1": "Test Name Ltd",
        "addressLine2": "123 Street",
        "town": "Town",
        "county": "County",
        "postcode": "UK1",
        "country": "UK"
      },
      "regulator": "Regulator",
      "regulatorEmail": "regulator@email.com"
    },
    "obligationYear": 2026,
    "obligations": [
      {
        "material": "Plastic",
        "recyclingTarget": 0.75,
        "tonnages": {
          "material": 100,
          "awaitingAcceptance": 10,
          "accepted": 2,
          "outstanding": 20,
          "obligated": 5
        },
        "status": "NoDataYet"
      }
    ],
    "obligationStatus": "NotMet",
    "submitterName": "Submitter Name",
    "audit": [
      {
        "action": "Submitted",
        "user": {
          "id": "e72be574-8b5b-4836-af47-dd7e0c0d1d87",
          "email": "submitter@email.com",
          "name": "Submitter Name"
        },
        "timestamp": "2026-01-02T03:04:05+00:00"
      },
      {
        "action": "Accepted",
        "user": {
          "id": "e72be574-8b5b-4836-af47-dd7e0c0d1d87",
          "email": "submitter@email.com",
          "name": "Submitter Name"
        },
        "timestamp": "2026-01-02T03:05:05+00:00",
        "reason": "Accepted reason"
      }
    ],
    "isRegulation43Compliant": true
  },
  "schemaVersion": "compliance_declaration.v1.0"
}
```
