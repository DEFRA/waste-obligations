# Mongo migrations during deployments

Mongo migrations are run by an API `BackgroundService`. The API begins listening before migration work starts, so a migration waiting for a lease—or a database operation that takes longer than expected—does not prevent ECS from receiving a successful `/health` response and progressing the rolling deployment.

The migration worker is deliberately separate from request readiness. Mongo migrations must therefore remain backwards-compatible with the previous application version while old and new ECS tasks overlap. Use expand/backfill/contract deployments for breaking changes.

## Lease and attempt behaviour

| Setting | Default | Purpose |
| --- | ---: | --- |
| `MongoMigrations:LeaseDurationSeconds` | 60 seconds | Recovery period after a host disappears without releasing its lease. |
| `MongoMigrations:LeaseRenewalIntervalSeconds` | 15 seconds | Keeps the lease exclusive while its host is alive, including while Mongo work is slow. |
| `MongoMigrations:AttemptTimeoutSeconds` | 5 minutes | Maximum time allowed for one complete migration-engine run. |
| `MongoMigrations:RetryDelaySeconds` | 30 seconds | Delay before rerunning a cancelled or failed engine. |
| `MongoMigrations:LeaseAcquisitionAlertThresholdSeconds` | 5 minutes | Wait before logging an error that the migration lease remains unavailable; retries continue. |
| `MongoMigrations:MaximumAttempts` | 3 | Limits retries by one host and therefore limits repeated error logs. |

The lease renewal interval must be no more than half the lease duration. This leaves at least half a lease period for a delayed renewal before another host can acquire the lease.

Only the host that owns `_migrations_lease` runs the migration engine. Its lease is renewed while that engine is running and while it waits between attempts. The lease is not version-preempted: old and new ECS tasks intentionally coexist during a rolling deployment, and forcibly taking a lease based on `SERVICE_VERSION` could allow two migration engines to work concurrently. `SERVICE_VERSION` remains deployment-log metadata, not coordination or fencing state.

Each retry creates a new `AdaskoTheBeAsT.MongoDbMigrations` engine and reruns the complete package. The package skips migrations already recorded in `_migrations`; an incomplete migration is run again. Current migrations are restartable under this exclusive lease, including the shared named-index helper, so this is the intended reconciliation mechanism rather than a migration-specific recovery method.

## A slow or hung migration

1. The lease owner starts one migration-engine attempt and starts the renewal task.
2. If it completes within five minutes, the worker releases the lease as normal.
3. At five minutes, the worker writes one error log with the attempt number and timeout, cancels the engine token, and continues renewing its lease.
4. It does **not** start a second engine until the original `RunAsync` task has returned. This preserves exclusive execution even if the Mongo driver or server has not stopped the underlying operation yet.
5. If the engine observes cancellation or returns an error, the same owner waits 30 seconds and runs the full package again. It keeps the lease throughout. After three unsuccessful attempts, it writes an error and stops attempting on that host.
6. If the engine does not return after cancellation, it remains the sole owner and the renewal task continues. This is not treated as a successful retry: the timeout error is the operational signal to investigate the Mongo operation. The API remains healthy for ECS, so this condition cannot deadlock the deployment.

On graceful ECS shutdown, the worker cancels the engine and uses an independent, ten-second token to release the lease. It does not use the already-cancelled host token for cleanup. If the task is killed or Mongo cannot accept the release, renewal stops and a replacement task can acquire the lease after at most 60 seconds.

No metrics are emitted for this worker. Errors are logged for the first acquisition failure, lease wait beyond the configured alert threshold, a timeout, renewal loss, and final attempt exhaustion. Configure the existing error-log alerting to notify the on-call team for those error messages without alerting for normal lease contention.

## September 2026 deployment incident

At `12:06:05`, one `0.117.0` task acquired the lease. The other two tasks correctly waited. That owner did not finish migration 012 before the later deployment action; a second `0.117.0` task acquired the lease at `14:06:06`, just over the original two-hour lease expiry, and completed migration 012 in about half a second. The subsequent completion proves the index had not already been recorded and makes data volume an unlikely explanation for the original 101-minute wait.

With the current model, the new task would have served `/health` immediately. At about `12:11`, it would have logged the timeout and requested cancellation. If cancellation returned, the same exclusive owner would retry the complete idempotent package. If it did not return, the renewed one-minute lease would preserve exclusivity while the timeout log identifies the exact condition for investigation; it would not block ECS from bringing healthy tasks online. A later ECS task termination would end renewal, reducing recovery from the previous two hours to at most one minute.

When a timeout occurs, inspect the Mongo server while the operation is live. On deployments that support `$currentOp`, this finds active index-build work or commit-quorum waits:

```javascript
db.getSiblingDB("admin").aggregate([
  { $currentOp: { allUsers: true, idleConnections: true } },
  {
    $match: {
      $or: [
        { "command.createIndexes": { $exists: true } },
        { op: "none", msg: /^Index Build/ }
      ]
    }
  }
])
```

The database role must be permitted to use `$currentOp`; the available fields vary by Mongo-compatible service. Retain the timed-out task's instance id, service version, timestamps, and error log when escalating to the database platform team.
