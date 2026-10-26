# Application data corrections

Use a schema migration for persisted shape changes and schema-version backfills. Use an application data correction when existing entities need an application-level update that analytics consumers must observe. A correction must call the owning application service; it must not write entities or construct audit events itself.

## Execution

`ApplicationDataCorrectionWorker` waits for this host's schema migration service to report success. It then acquires the private `_application_data_correction_lease` lease. Schema migration attempts and correction execution have separate leases and lifetimes. Neither blocks API startup or readiness. Failed schema migrations leave corrections waiting rather than allowing a false completion marker.

Corrections declare a stable ID and a positive integer version independent of entity schema versions. The runner sorts by version and rejects duplicate IDs or versions before executing work. Add new corrections at increasing versions; never change the identity or transformation of a completed correction. Failures stop the ordered run so dependent corrections do not execute. Completion records in `_application_data_corrections` contain the ID, version and completion timestamp and are written only after successful execution.

A renewable lease coordinates hosts. Lease loss requests cancellation; the worker waits for its work to stop before releasing ownership. The worker retries failures independently of schema migrations. It has no whole-pass timeout: a larger lower-environment scan can continue beyond the migration attempt budget. `ApplicationDataCorrections` settings control lease duration, renewal interval, retry delay and scan batch size; defaults require no new environment-specific values or journey configuration.

The coverage correction scans using bounded `_id` keyset pages and the built-in Mongo index. Interrupted passes restart at the beginning. This deliberately favours a small runner over durable per-item progress at the current volume. Already-correct records perform no write and produce no duplicate events. If future corrections need durable progress, record it only after each entity update commits; a completion marker alone is not a resumable cursor.

## Shared service updates and audit capture

`ComplianceDeclarationCoverageCorrection` loads current-schema declarations as typed entities through `IDbContext`. It calculates coverage, compares the result with the stored percentage and creates an updated entity only when the value changes. It calls the existing `IComplianceDeclarationService.Update` method with explicit service attribution. A missing or null percentage becoming numeric is a change. Unchanged values cause no service update, version increment or event.

The service owns optimistic concurrency, the version increment, UTC update timestamp, eligibility visibility refresh, transactional audit capture, metrics and logging. The optional actor argument lets background corrections identify the service; request callers retain attribution from the declaration's audit user. Corrections do not fabricate a user, change business audit entries or status, or send email.

Corrections use the same typed entity replacement as ordinary updates. There is no separate BSON mutation path or correction-specific method on the service. Audit snapshots are produced by the existing service update flow and the entity update and `submission.amended` outbox event commit together. Audit failure rolls back the update. This pattern targets documents matching the current entity schema; it does not preserve fields unknown to that model. Use a schema migration for incompatible stored shapes rather than adding BSON manipulation to routine corrections.

Audit capture does not depend on analytics publication being enabled. The existing dispatcher can publish captured events later using each event's recorded schema. Historical audit events and dispatch state are never rewritten.

## Coverage calculation and rollout

The active calculator averages participating individual-material percentages. Historical migrations retain the aggregate-tonnage calculator so their outcomes do not change when active logic changes. This calculation correction preserves the field's type, representation and percentage contract, so declarations and new events remain on schema v1.3. No persisted entity shape or embedded schema changes are introduced.

The one-time correction applies to existing v1.3 declarations across statuses and years. Historical unsubmitted summary backfills are outside this change. Old hosts may still write the old calculation during a rolling deployment; the completion marker does not provide recurring reconciliation. Coordinate rollout if that overlap is unacceptable. Rolling back code does not reverse committed corrections or their audit events.

## Verification boundary

Calculation and endpoint contract tests cover the numeric change. Mongo integration tests cover typed entity updates, shared service behaviour, service attribution, atomic capture, concurrency, interrupted execution, completion and index use. Worker tests cover migration gating, explicit ordering, independent lease renewal and cancellation. No endpoint, authentication, external dependency or browser navigation contract changes, so no companion journey change is required.
