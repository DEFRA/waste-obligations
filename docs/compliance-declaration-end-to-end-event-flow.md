# Compliance declaration event flow

Start with the [audit foundation](audit-events.md) for transactional capture and the [shared analytics guide](analytics-events.md) for projection and delivery. The [declaration consumer guide](analytics-compliance-declaration-events.md) contains entity-specific vocabulary, schema history and coverage-correction constraints.

## Two distinct audit concepts

A declaration's embedded `audit` array preserves business history of status actions. The `AuditEvent` collection is an outbox for downstream publication. They serve different purposes even when a declaration change updates both in one transaction. The [declaration entity](../src/Api/Data/Entities/ComplianceDeclaration.cs) and [service](../src/Api/Services/ComplianceDeclarationService.cs) define this behavior.

Email delivery is outside the Mongo transaction. The [email delivery cutover guide](email-delivery-cutover.md) owns its separate cross-service activation and recovery constraints; analytics delivery does not change them.
