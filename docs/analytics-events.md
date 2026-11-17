# Analytics events

The service publishes entity changes to the analytics SNS topic configured by `AnalyticsAuditEventProcessor:TopicArn`. Every entity type uses the same envelope, transport, vocabulary validation and delivery guarantees described here. Entity-specific identities, event types, actors, correlation and payload schemas are described in each entity's page:

- [Compliance declaration events](analytics-compliance-declaration-events.md)
- [Obligation calculation events](analytics-obligation-calculation-events.md)

## Capture and delivery

An entity change and its analytics outbox event are written in the same Mongo transaction, so a failed write leaves no event. The [analytics processor](../src/AuditEvents/Analytics/AnalyticsAuditEventProcessor.cs) reads undispatched events, serialises them, publishes them to SNS and records the dispatch outcome only after SNS accepts the message, applying its retry, lease and dead-letter behaviour. The [end-to-end event flow](compliance-declaration-end-to-end-event-flow.md) shows this path for compliance declarations.

Delivery is at least once: publication can succeed before the dispatch outcome is recorded, so a message can be repeated with the same `eventId`. Consumers must make ingestion idempotent using `eventId`.

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
  "operation": "create",
  "eventType": "domain.event",
  "deletedReason": null,
  "piiKeyRef": null,
  "occurredAt": "2026-01-02T03:04:05.000Z",
  "recordedAt": "2026-01-02T03:04:06.000Z",
  "actor": "user:e72be574-8b5b-4836-af47-dd7e0c0d1d87",
  "correlationId": "cdp-request-id",
  "version": 1,
  "before": null,
  "after": {},
  "schemaVersion": "entity_name_v1.0"
}
```

| Field | Description |
| --- | --- |
| `eventId` | ULID generated for the analytics event. |
| `sequence` | Global analytics event sequence allocated when the event is written. |
| `entity` | Logical entity type for the payload. |
| `entityId` | Unique identifier for the entity instance, with an entity-specific prefix. |
| `operation` | Event operation. See the operation values below. |
| `eventType` | Business event name registered for the entity. |
| `deletedReason` | Reason the entity was deleted. This is only set when `operation` is `delete`; otherwise it is `null`. |
| `piiKeyRef` | Always `null`; PII classification and protection are outside the scope of this change. |
| `occurredAt` | Time the entity change occurred, as a UTC ISO 8601 timestamp with millisecond precision. |
| `recordedAt` | Time the analytics event was recorded, as a UTC ISO 8601 timestamp with millisecond precision. |
| `actor` | Who or what made the change, as `<prefix>:<identifier>`. |
| `correlationId` | Correlation ID for the request or run that made the change; omitted when none is available. |
| `version` | Entity version after the operation. |
| `before` | Previous entity state. This is `null` for creates. |
| `after` | New entity state. This is `null` for deletes. |
| `schemaVersion` | Entity-qualified schema version, `<entity>_<version>`, for the `before` and `after` payloads. |

## Operation values

| Operation | Meaning | `before` | `after` | `deletedReason` |
| --- | --- | --- | --- | --- |
| `create` | Entity was created. | `null` | Created entity state. | `null` |
| `update` | Entity was changed. | Previous entity state. | Updated entity state. | `null` |
| `delete` | Entity was deleted. | Previous entity state. | `null` | Delete reason. |

## Controlled vocabulary

The publisher validates each event against the [analytics vocabulary](../src/AuditEvents/Analytics/AnalyticsEventVocabulary.cs) before serialising it. Each entity registers its entity ID prefix, permitted operations, event types and raw schema versions; an event whose entity, operation, event type, actor prefix, deletion reason or schema version is not registered cannot be published.

Actor prefixes are `service`, `user`, `system`, and `integration`. The only current deletion reason is `elevated_system_allowed_removal`. This replaces the legacy free-text value `elevated system allowed removal`, which remains normalised for undispatched historical outbox events. The governed list maintained with the shared event schema must include every registered value before it is published.
