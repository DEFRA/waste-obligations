# Analytics compliance declaration events

This is the declaration-specific companion to the [shared analytics guide](analytics-events.md). The [audit foundation](audit-events.md) explains transactional capture; the [declaration flow](compliance-declaration-end-to-end-event-flow.md) distinguishes the outbox from embedded business history.

## Consumer meaning

In event names, “submission” means a compliance declaration change, not another business record. A declaration's analytics identity is stable across its versions. The [declaration contract](../src/AuditEvents/Analytics/ComplianceDeclarationEventContract.cs) defines the entity name, identity prefix and create, amendment and removal event names. The [declaration service](../src/Api/Services/ComplianceDeclarationService.cs) defines the snapshots represented by those changes; [SNS contract tests](../tests/Api.IntegrationTests/AuditEvents/Analytics/SnsAnalyticsEventSenderTests.cs) exercise the published messages.

Request-driven changes identify the responsible audit user. System-driven corrections and removals use the service actor from [AuditActors](../src/Api/Utils/Logging/AuditActors.cs). Request correlation is included when available.

The governed removal reason is `elevated_system_allowed_removal`. Historical outbox events may contain the earlier phrase `elevated system allowed removal`; the [shared vocabulary normalizer](../src/AuditEvents/Analytics/AnalyticsEventVocabulary.cs) preserves their publishability. Register the governed reason externally before introducing additional removal paths.

## Payload schema and coverage correction

The [entity](../src/Api/Data/Entities/ComplianceDeclaration.cs) owns the current raw schema version. Current declarations use `v1.3`, published as `compliance_declaration_v1.3`; the [v1.3 schema](../src/Api/Schemas/ComplianceDeclaration/compliance-declaration.v1.3.schema.json) and [schema changelog](../src/Api/Schemas/ComplianceDeclaration/CHANGELOG.md) define the payload and its compatibility history. Use the shared guide for historical-schema and delivery guarantees rather than treating this current version as applying to every stored event.

The [coverage calculator](../src/Api/Data/Entities/ObligationCoveragePercentageCalculator.cs) and its [tests](../tests/Api.Tests/Data/Entities/ObligationCoveragePercentageCalculatorTests.cs) define the calculation. The one-time [application data correction](application-data-corrections.md) changes affected declarations through the normal service, producing new events without rewriting historical snapshots. Older hosts can still write the previous formula during rollout. Rolling back code does not restore corrected values, and there is no operational reversal of the correction.
