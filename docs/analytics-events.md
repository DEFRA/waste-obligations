# Analytics events

Analytics messages are a projection of committed [audit events](audit-events.md), not a second source of business state. Start with the audit foundation for capture guarantees, then use the entity guides for [declarations](analytics-compliance-declaration-events.md) or [calculations](analytics-obligation-calculation-events.md).

## Projection and schemas

The [envelope](../src/AuditEvents/Analytics/AnalyticsEvent.cs) and [mapper](../src/AuditEvents/Analytics/AnalyticsEventMappers.cs) define the wire fields. New audit records carry the producer's analytics identity; historical records use the legacy identity in the [entity registrations](../src/AuditEvents/Analytics/AnalyticsEventVocabulary.cs). Capturing identity at write time prevents later naming changes from changing an already-recorded message.

The mapper retains the recorded snapshots and qualifies their raw schema version with the entity name. The [serializer](../src/AuditEvents/Analytics/JsonAnalyticsEventSerializer.cs) uses the [embedded schema provider](../src/Api/Schemas/EmbeddedEntityJsonSchemaProvider.cs) before publication. Embedded versioned schemas are the local supported-version authority: keep historical schemas available and never rewrite historical events to a newer version. Entity version and payload schema version have different meanings; see [schema change guidance](../CONTRIBUTING.md#persisted-entity-and-schema-changes).

The [vocabulary](../src/AuditEvents/Analytics/AnalyticsEventVocabulary.cs) governs entities, operations, event types, actor prefixes and deletion reasons. [Actor helpers](../src/Api/Utils/Logging/AuditActors.cs) centralize producer identities. Snapshot values are preserved rather than recalculated for analytics; `piiKeyRef` remains null. Envelope times are UTC with millisecond precision, and recording time cannot precede occurrence time.

## Delivery and consumers

The [processor](../src/AuditEvents/Analytics/AnalyticsAuditEventProcessor.cs), [dispatch service](../src/AuditEvents/AuditEventDispatchService.cs) and [lease service](../src/AuditEvents/AuditEventLeaseService.cs) implement asynchronous delivery, retry and dead-letter state. Dispatch is successful only after SNS accepts the message. A failure before the successful outcome is recorded can cause another publication: consumers must deduplicate by `eventId`. The global `sequence` includes every entity type, so gaps in one entity's stream do not imply missing versions.

The [SNS sender](../src/AuditEvents/Analytics/SnsAnalyticsEventSender.cs) and its [contract tests](../tests/Api.IntegrationTests/AuditEvents/Analytics/SnsAnalyticsEventSenderTests.cs) define transport. Messages use `Content-Type: application/json`; compressed bodies use `Content-Encoding: gzip+base64`. Consumers must decode the indicated encoding before parsing JSON. Topic, polling and retry settings are defined by the [processor options](../src/AuditEvents/Analytics/AnalyticsAuditEventProcessorOptions.cs). The optional [queue consumer](../src/Api/Consumers/AnalyticsAuditEventConsumer.cs) is not required for capture or publication.

Dead-lettering retains a failed delivery outcome; it is not evidence of a completed recovery. SNS acceptance cannot be retracted by rolling back the application. Coordinate any correction or replay with consumers; see the [outbox ADR](adr/0008-transactional-analytics-outbox.md) for the delivery trade-off.

## Adding or evolving an entity integration

Use the existing producer pattern: record snapshots, raw identity, analytics identity and the entity's own schema version in its transaction. Add vocabulary registration data rather than an entity branch in the mapper. Add immutable versioned schema resources and retain prior resources as required by the [contribution workflow](../CONTRIBUTING.md#persisted-entity-and-schema-changes). Cover the changed persistence and consumer boundary, including historical compatibility.

Register the entity, event names, schema versions and actor prefix in the shared governed vocabulary and confirm consumer readiness before publication. Local registration and embedded schemas do not prove that external registration or readiness is complete. Entity-specific rollout constraints belong in the corresponding guide below this shared layer.
