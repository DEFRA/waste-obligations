# Email delivery cutover

`EmailDelivery:EmailDeliveryCutoverUtc` is nullable and defaults to `null`.
When it is null or absent, Waste Obligations retains its current submitted
and cancellation email behaviour.

When configured, Waste Obligations sends only for actions before the cutover.
It suppresses email for actions exactly at or after the cutover, before
resolving cancellation recipients or recording email-send metrics. Declaration
creation and cancellation still succeed and retain their existing responses,
audit entries and analytics events. If the relevant action audit entry is
missing, the service logs a warning and suppresses the email without failing
the declaration operation.

The action time comes from the immutable `Submitted` or `Cancelled` audit
entry, respectively. It does not come from `Created`, `Updated`, the current
time or analytics processing time. Both action and cutover timestamps are
truncated to whole milliseconds, matching Mongo storage and Notifications.
This also applies when an in-memory action has finer precision before its
Mongo roundtrip.

The configured value must be an ISO timestamp with an explicit UTC timezone:
`Z`, `+00:00` or `-00:00`. Empty, malformed, offset-free and nonzero-offset
values fail startup. For example:

```json
{
  "EmailDelivery": {
    "EmailDeliveryCutoverUtc": "2027-01-01T00:00:00Z"
  }
}
```

The environment variable is `EmailDelivery__EmailDeliveryCutoverUtc`.
Notifications uses
`NotificationCommandDelivery__EmailDeliveryCutoverUtc` and makes the inverse
decision: it sends commands for actions at or after the same instant.

## Activation and rollback

Leave the setting null until submitted and cancellation notification
initiation is ready under MO-549 and MO-550. MO-561 provides delivery but does
not initiate declaration notifications. Activating this cutoff before those
producers are ready would suppress emails without a replacement send.

The deployment owners must configure both services with the identical future
UTC instant before it takes effect and verify the deployed CDP configuration
separately. This PR adds no CDP or Azure configuration and no production
cutover date. There is no entity or analytics schema change, migration, queue
publication or rewrite of historical audit events.

Restarting Waste Obligations hosts with this setting null restores the direct
email path for subsequent calls. Pause or otherwise coordinate the Notifications path first to avoid
both services sending for the same action. This service does not replay
emails it previously suppressed.

## Journey coverage

The shared journey CI Compose stack and this service's local Compose stack
leave the new setting unset, inheriting the null default. No runner variable,
secret injection, WireMock mapping, seed data or companion branch is needed
for that unchanged journey. Existing journey scenarios continue to exercise
submission and cancellation; service tests cover the configured boundary and
HTTP response contracts. The handover journey needs validation with the
replacement producers and their configuration when that migration is ready.

## References

- [Notifications PR 5: Pre-cutover suppression](https://github.com/DEFRA/waste-obligations-notifications/pull/5)
- [Notifications PR 8: Post-cutover delivery](https://github.com/DEFRA/waste-obligations-notifications/pull/8)
- [Notifications PR 9: Command recovery](https://github.com/DEFRA/waste-obligations-notifications/pull/9)
