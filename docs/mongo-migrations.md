# Mongo migrations during deployments

Mongo migrations run in an API `BackgroundService` and do not block host startup or readiness. The API can listen and serve `/health` while migrations are pending, so slow migrations do not prevent ECS from progressing a rolling deployment. There is no strict ordering between the first migration work and the API beginning to listen.

The migration worker is deliberately separate from request readiness. Mongo migrations must therefore remain backwards-compatible with the previous application version while old and new ECS tasks overlap. Use expand/backfill/contract deployments for breaking changes.

## Lease and attempt behaviour

The [migration worker](../src/Api/Data/MongoMigrationService.cs), [lease service](../src/Api/Data/MongoMigrationLeaseService.cs), [options](../src/Api/Data/MongoMigrationOptions.cs), and [tests](../tests/Api.Tests/Data/MongoMigrationServiceTests.cs) define the current deadlines, retries and coordination. A cancellation deadline is not a hard kill: an operation that does not drain requires investigation, and the host must not start a competing attempt while its original work is still running.

Lease ownership is not preempted by a newer service version: old and new hosts coexist during a rolling deployment. A lease is not fencing, so expiry after connectivity loss cannot prevent non-cooperative work from overlapping with another host. Configure error-log alerting for timeouts, renewal loss and exhausted attempts; normal lease contention is not an error.

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
