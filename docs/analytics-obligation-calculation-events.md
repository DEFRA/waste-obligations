# Analytics obligation calculation events

This is the calculation-specific companion to the [shared analytics guide](analytics-events.md), built on transactional [audit events](audit-events.md). These messages describe shadow calculations for a submitter organisation and obligation year; they do not replace public obligation reads from the PRN Common Backend.

## Consumer meaning

The [calculation contract](../src/AuditEvents/Analytics/ObligationCalculationEventContract.cs) defines `obligation_calculation`, its stable `oblcalc_` identity prefix and the create/update event names. A create carries the first saved result with no prior snapshot. An update carries the actual previous result and the new version under the same identity. Recalculation does not emit deletion events. The [persistence service](../src/Api/Services/ObligationCalculations/ObligationCalculationService.cs) and its [Mongo tests](../tests/Api.IntegrationTests/Services/ObligationCalculations/ObligationCalculationServiceTests.cs) define write, equivalence and rollback behavior.

The [entity](../src/Api/Data/Entities/ObligationCalculation.cs) owns raw schema version `v1.0`, published as `obligation_calculation_v1.0`. Both snapshots use the recorded version's [embedded schema](../src/Api/Schemas/ObligationCalculation/obligation-calculation.v1.0.schema.json); see its [changelog](../src/Api/Schemas/ObligationCalculation/CHANGELOG.md). Producer and material values, including separate `GL` and `GR` results, are preserved rather than recomputed during publication. The [SNS contract tests](../tests/Api.IntegrationTests/AuditEvents/Analytics/SnsAnalyticsEventSenderTests.cs) exercise both messages with schema-bound payloads.

The actor is the system identity in [AuditActors](../src/Api/Utils/Logging/AuditActors.cs), and occurrence time is calculation completion time. Correlation identifies the originating request or scheduled instruction run. [Instruction creation](../src/Api/Services/ObligationCalculations/ObligationCalculationInstructionService.cs) retains an originating trace or generates one; the [work source](../src/Api/ScheduledJobs/ObligationCalculationWorkSource.cs) repairs missing legacy traces before processing and retains them across retries.

## Rollout constraints

There is no initial backfill. An existing calculation may first appear as an update, or remain absent until materially changed. Consumers must not require a create message before accepting an update.

Older application instances can write calculations without events during a mixed-version rollout. Coordinate activation with the consumer and do not treat the stream as complete across that interval. The shared guide's governance, duplicate handling and irreversible SNS-acceptance constraints also apply.
