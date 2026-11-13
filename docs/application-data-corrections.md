# Application data corrections

Use schema migrations for persisted shape changes and schema-version backfills. Use application data corrections when existing entities need application-level updates that analytics consumers must observe. Corrections call the owning application service rather than writing BSON directly or fabricating audit events; typed replacement cannot preserve fields unknown to the model.

The [correction runner](../src/Api/Data/ApplicationDataCorrectionRunner.cs), [worker](../src/Api/Data/ApplicationDataCorrectionWorker.cs), [options](../src/Api/Data/ApplicationDataCorrectionOptions.cs), and their [unit](../tests/Api.Tests/Data/ApplicationDataCorrectionRunnerTests.cs) and [integration tests](../tests/Api.IntegrationTests/Data/ApplicationDataCorrectionTests.cs) define execution, ordering, retries and lease handling. Add corrections with increasing versions and stable identities; do not change a completed correction's identity or transformation. Interrupted passes must be safe to restart.

Corrections wait for successful local schema migrations and do not block API startup or readiness. A watchdog requests cancellation but cannot force a non-cooperative operation to stop; investigate an operation that does not drain. Lease expiry cannot fence work from a host that has lost connectivity.

## Obligation coverage correction

The [coverage correction](../src/Api/Data/Corrections/001_ComplianceDeclarationCoverageCorrection.cs) and its [tests](../tests/Api.Tests/Data/Corrections/ComplianceDeclarationCoverageCorrectionTests.cs) retain the original individual-material calculation. It corrects the existing metric without changing the v1.3 schema contract. Earlier migrations retain their historical calculators, and historical audit events are not rewritten.

Changed declarations pass through the normal service and produce new analytics events; unchanged declarations do not. Updated timestamps may change consumer sort order. Old hosts can still write the previous formula during rollout, and no recurring reconciliation is provided. Rolling back code does not reverse committed corrections or emitted events. Historical unsubmitted summaries are not backfilled by this correction; normal summary refreshes use the active calculator.


## Glass coverage correction

The [glass correction](../src/Api/Data/Corrections/002_GlassCoverageCorrection.cs) recalculates current-schema declarations from their saved obligations using the [coverage calculator](../src/Api/Data/Entities/ObligationCoveragePercentageCalculator.cs). Glass and GlassRemelt contribute one combined coverage value. It uses the same audited service update, retries and rollout limitations as the earlier correction. Check completion and failures in the correction worker logs; a host running the previous calculation can overwrite a corrected value during a rolling deployment, so a further pass may be needed after old hosts have drained.

Unsubmitted summaries do not store individual material tonnages. Active summaries are recalculated from the PRN source on successful [hydration](../src/Api/Services/OrganisationObligations/OrganisationObligationHydrationService.cs), subject to the polling configuration, schedule and retries. Inactive historical summaries are not corrected by this declaration correction; they require a separate source refresh. Historical backfill completion cannot simply be restarted.
