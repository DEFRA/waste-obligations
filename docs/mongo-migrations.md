# Mongo migrations during deployments

Mongo migrations run in an API `BackgroundService` and do not block host startup or readiness. The API can listen and serve `/health` while migrations are pending, so slow migrations do not prevent ECS from progressing a rolling deployment. There is no strict ordering between the first migration work and the API beginning to listen.

The migration worker is deliberately separate from request readiness. Mongo migrations must therefore remain backwards-compatible with the previous application version while old and new ECS tasks overlap. Use expand/backfill/contract deployments for breaking changes.

## Lease and attempt behaviour

| Setting | Default | Purpose |
| --- | ---: | --- |
| `MongoMigrations:LeaseDurationSeconds` | 60 seconds | Recovery period after a host disappears without releasing its lease. |
| `MongoMigrations:LeaseRenewalIntervalSeconds` | 15 seconds | Interval between lease renewals while the owner is processing work. |
| `MongoMigrations:LeaseOperationTimeoutSeconds` | 10 seconds | Threshold for reporting and cancelling an acquisition or renewal call that has not returned. |
| `MongoMigrations:AttemptTimeoutSeconds` | 5 minutes | Threshold for logging a stalled attempt and requesting cancellation; execution can take longer to stop. |
| `MongoMigrations:RetryDelaySeconds` | 30 seconds | Delay before rerunning a cancelled or failed engine. |
| `MongoMigrations:LeaseAcquisitionAlertThresholdSeconds` | 5 minutes | Wait before logging an error that the migration lease remains unavailable; retries continue. |
| `MongoMigrations:MaximumAttempts` | 3 | Limits engine attempts by one host, including attempts after reacquiring a lease. |

The renewal interval must be no more than half the lease duration. The renewal interval plus its operation timeout must also be less than the lease duration. These checks leave time to request cancellation before ownership expires; they cannot force a stalled client or server operation to terminate.

Only the host that owns `_migrations_lease` runs the migration engine. Its lease is renewed while that engine is running and while it waits between attempts. The lease is not version-preempted: old and new ECS tasks intentionally coexist during a rolling deployment, and forcibly taking a lease based on `SERVICE_VERSION` could allow two migration engines to work concurrently. `SERVICE_VERSION` remains deployment-log metadata, not coordination or fencing state.

Each retry creates a new `AdaskoTheBeAsT.MongoDbMigrations` engine and reruns the complete package. The package skips migrations already recorded in `_migrations`; an incomplete migration is run again. Current migrations are restartable under this exclusive lease, including the shared named-index helper, so this is the intended reconciliation mechanism rather than a migration-specific recovery method.

## A slow or hung migration

1. The lease owner starts one migration-engine attempt and starts the renewal task.
2. If it completes within five minutes, the worker releases the lease as normal.
3. At five minutes, the worker writes one error log with the attempt number and timeout, cancels the engine token, and continues renewing its lease.
4. It does **not** start a second engine until the original `RunAsync` task has returned. This preserves exclusive execution even if the Mongo driver or server has not stopped the underlying operation yet.
5. If the engine observes cancellation or returns an error, the same owner waits 30 seconds and runs the full package again. It keeps the lease throughout. After three unsuccessful attempts, it writes an error and stops attempting on that host.
6. If the engine does not return after cancellation, it remains the sole owner and the renewal task continues. This is not treated as a successful retry: the timeout error is the operational signal to investigate the Mongo operation. The API remains healthy for ECS, so this condition cannot deadlock the deployment.

On graceful ECS shutdown, the worker requests cancellation and waits for migration and renewal work to finish before releasing ownership. The subsequent release call has an independent ten-second watchdog. This is not a ten-second bound on the whole shutdown, and cancellation cannot force an uncooperative call to stop. If ECS terminates the process, renewal stops and the lease becomes eligible for takeover after its remaining TTL, normally at most 60 seconds; Mongo connectivity and acquisition time can delay actual recovery.

No metrics are emitted for this worker. Errors are logged for the first acquisition failure, lease wait beyond the configured alert threshold, an active-operation timeout, renewal loss, engine failures (including failures raised while draining), and final attempt exhaustion. A deadline reached after shutdown or cleanup cancellation has already been requested is logged as a warning that cancellation is still draining. Configure the existing error-log alerting to notify the on-call team for those error messages without alerting for normal lease contention.

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

## Shared watchdog and Mongo operation limits

`BackgroundOperationWatchdog` is used by schema migrations. It reports a timeout when the independent timer expires, requests cancellation and awaits the original task before the caller can retry or release its lease. A call that ignores cancellation remains pending and requires operational investigation; the watchdog is not a hard process or server-command kill. A late lease-acquisition response is rejected because ownership must be freshly acquired before starting work.

Acquisition, renewal and release calls have their own watchdogs. A stalled renewal immediately requests cancellation of owned work, rather than waiting indefinitely for Mongo to return a failure. Ownership is released only after owned work and the pending renewal finish. A renewable TTL lease still provides no fencing against non-cooperative work after connectivity is lost; no automatic overlapping retry is started by the original host.

The Mongo driver can fail connection and server-selection operations on its own timeouts, but those do not bound every socket read. Existing request-path Mongo settings are unchanged.

The watchdog deadline remains active while cancellation is draining, including during lease-renewal cleanup. Failures in timeout reporting or cancellation callbacks are retained, but never permit the original operation to be abandoned before it ends.

## Recovery and cleanup

An overdue renewal still cancels the migration work. A late successful renewal does not prove ownership is safe: Mongo computes expiry from the request start, so the response can arrive after that expiry. The host waits for both the engine and renewal to stop, attempts owner-filtered release, then acquires a fresh lease and creates new cancellation sources before retrying. `MaximumAttempts` applies across those acquisitions and does not reset each time.

A late successful acquisition is also released after its command drains, including during shutdown. Failed releases fall back to normal lease expiry. Cleanup attempts every step in order even if a cancellation callback or a completed renewal task faults; failures are retained and logged after release is attempted.

The watchdog awaits asynchronous timeout callbacks. It preserves both operation and callback failures instead of hiding them behind successful results. Cancellation caused by its deadline is reported as a timeout; caller cancellation remains cancellation. Callback failures can produce aggregate errors. All paths drain the original operation before returning or throwing.
