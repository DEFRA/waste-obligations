# Audit events

Audit events are the durable foundation for downstream analytics. A business transaction can change several documents; its producer records the entity state change represented by each event in the same Mongo transaction. Capture survives a later delivery failure. The [transactional outbox ADR](adr/0008-transactional-analytics-outbox.md) explains this boundary and its rationale.

## Capture contract

The [audit request](../src/AuditEvents/AuditEventRequest.cs), [stored event](../src/AuditEvents/Entities/AuditEvent.cs) and [recorder](../src/AuditEvents/AuditEventService.cs) define the capture contract. The producer supplies the actual snapshots, entity version, raw payload schema version, actor and originating correlation. The recorder allocates the global sequence and event identity within the transaction.

Current producers are the [declaration service](../src/Api/Services/ComplianceDeclarationService.cs) and [calculation service](../src/Api/Services/ObligationCalculations/ObligationCalculationService.cs). Their transaction and concurrency behavior is defined by those services and [MongoDbContext](../src/Api/Data/MongoDbContext.cs), rather than duplicated here.

An event's `SchemaVersion` describes its entity snapshots, not the storage shape of the outbox record. The raw `EntityId` remains the indexed audit identity. Optional internal `AnalyticsEntityId` metadata captures the producer's analytics identity without changing that raw identity or the admin JSON contract. Historical events without this metadata remain readable and use the registered legacy identity when projected.

The outbox is distinct from business audit history embedded in an entity. See the [declaration-specific flow](compliance-declaration-end-to-end-event-flow.md) for that distinction and the boundary with email delivery.

## Downstream use

The [analytics guide](analytics-events.md) describes projection, delivery guarantees and adding an entity integration. Entity-specific consumer meaning is documented for [declarations](analytics-compliance-declaration-events.md) and [calculations](analytics-obligation-calculation-events.md).
