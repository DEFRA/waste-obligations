# Application data corrections

Use schema migrations for persisted shape changes and schema-version backfills. Use application data corrections when existing entities need application-level updates that analytics consumers must observe.

## Layout and ordering

The runtime types, options, completion record and contracts live directly in `src/Api/Data`. `src/Api/Data/Corrections` contains only concrete application data corrections. Name those files `NNN_Description.cs`, matching the migration convention, and match the numeric prefix to the correction's explicit `Version`.

The runner still orders by `Version`; filenames make that order visible in the repository and do not replace runtime ordering or validation.

## Execution

`ApplicationDataCorrectionWorker` waits for successful schema migrations, then acquires its own renewable lease in `_application_data_correction_lease`. Neither worker blocks API startup or readiness. The coverage correction is registered as version 1.

Corrections declare a stable ID and positive integer version. The runner sorts by version and rejects duplicate IDs or versions. It writes a completion record to `_application_data_corrections` only after a successful pass. Completion reads use the primary so replication lag cannot invalidate coordination. Add new corrections with increasing versions; do not change a completed correction's identity or transformation.

A failed correction stops the ordered run. The worker retries independently of migrations. Interrupted passes must be safe to restart: a correction should compare the proposed entity values and skip unchanged entities before calling the owning application service.

There is no whole-pass deadline. `OperationTimeoutSeconds` defaults to 30 seconds for completion reads and writes; domain corrections should use it for bounded reads and service updates too. Lease commands have a separate 10-second cancellation threshold. A watchdog reports overdue work, requests cancellation and awaits the original operation before retry or release. Non-cooperative operations can require operator intervention. TTL leases provide no server-side fencing after connectivity is lost.

## Service boundary

The correction loads compatible strongly typed entities through the existing database context, constructs the updated entity, and calls its service's normal update method only when values changed. It must not replace service behaviour with direct BSON writes or construct audit events itself.

`IComplianceDeclarationService.Update` accepts explicit service attribution for background changes. It retains concurrency checks, version increments, timestamps, eligibility refresh, metrics and transactional audit capture. The correction must not fabricate a user or change business audit entries. Audit capture remains independent of analytics publication.

Typed replacement does not preserve fields unknown to the model. Use schema migrations for incompatible stored shapes and leave historical audit snapshots unchanged.

## Verification

Worker tests cover migration gating, lease renewal, cancellation and timeout draining. Runner unit and Mongo integration tests cover explicit ordering, completion, independent lease ownership and primary completion reads. Integration queries are checked by the existing profiler.

## Obligation coverage correction

`001_ComplianceDeclarationCoverageCorrection.cs` declares version 1 and scans compatible declarations in bounded `_id` pages using primary reads, so replication lag cannot make the pass miss migrated documents and record a false completion. It calculates the proposed percentage and calls the shared service only when the numeric value differs. Unchanged declarations create no update, version increment or audit event. Null percentages becoming numeric values are changes.

The active calculation averages exact, individually capped material percentages and rounds the final average to a whole number away from zero. `NoDataYet` rows do not participate. A `Met` row with both accepted and obligated tonnage zero contributes 100%, as required by MO-661 AC2. Positive accepted tonnage with zero obligation retains the existing implementation's 0% treatment pending a separate business clarification.

This fixes the implementation of the existing percentage-met metric rather than changing its intended meaning, type or persisted representation. Schema v1.3 is retained. The historical aggregate calculator remains with migrations 004 and 005; published schemas and historical audit events are unchanged. The deployment containing this correction is the calculation cutover, while retained historical events continue to describe their original values.

Each changed declaration and its `submission.amended` audit event commit together through the service, with a version increment, updated timestamp and explicit service actor. The correction preserves business audit entries and does not fabricate a user. Changing `Updated` also changes ordering for consumers sorting by that field.

An interrupted scan restarts from the beginning and skips already-correct values. Old hosts can still write the old formula during a rolling deployment; recurring reconciliation is outside this correction. Code rollback does not reverse committed corrections or emitted events. Historical unsubmitted summaries are not backfilled by this declaration correction; normal summary refreshes use the active calculator.

Mongo integration tests cover changed and unchanged declarations, service attribution, audit rollback, concurrency, primary reads, retry idempotency and compatible-schema filtering. The historical-event comparison excludes mutable dispatch state while protecting the original event content.
