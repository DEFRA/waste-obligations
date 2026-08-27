# Unsubmitted compliance declarations: organisation snapshot and query design

## Status and scope

**Status:** the initial server-side delivery is implemented in this branch: the local eligibility snapshot, Account-reference materialisation, declaration-review-state projection, unsubmitted query endpoint with generic search, and organisation-obligation summary hydration/polling. The later event-driven and operational-insight sections remain future design considerations. `Unsubmitted` remains an inferred review state rather than a compliance-declaration status.

The proposed delivery is a local, refreshable copy of the Waste Organisations eligibility data in Waste Obligations, a local declaration-presence projection maintained as declarations change, and a separately refreshed organisation-obligation summary. Together they support a server-side query for the **Not submitted** review tab and CSV download.

Account reference resolution is a prerequisite for an individual organisation to enter the queryable unsubmitted view: a source row with no resolved reference is stored and retried, but is not considered. Its value is materialised into the organisation generation rather than hidden behind request-time calls. An organisation's current obligations have a materially different freshness contract, so percentage met is held in a separate, per-organisation/year projection rather than being folded into an organisation generation.

## Decisions already made

- `Unsubmitted` is **not** a new `ComplianceDeclaration` status and is not persisted on a declaration.
- It is an inferred review state for an organisation, a registration type, and an obligation year.
- The first source to bring into Waste Obligations is Waste Organisations.
- Waste Organisations is unchanged: its existing, unpaged search endpoint is the source.
- The initial public query route is `GET /compliance-declarations/unsubmitted`.
- The new local review projection will track `LARGE_PRODUCER` as `DirectProducer` and `COMPLIANCE_SCHEME` as `ComplianceScheme`.

## Current compliance year

Current-obligation hydration is deliberately limited to the **current compliance year**. The compliance year runs from 1 February to 31 January, so calculate it from the business date in the UK time zone:

```text
currentComplianceYear = localDate.Month == January
  ? localDate.Year - 1
  : localDate.Year
```

For example, every day from 1 to 31 January 2027 has current compliance year `2026`; 1 February 2027 changes it to `2027`. Implement this behind one tested domain service using an injected `TimeProvider` and an explicit `Europe/London` business time zone, never the host-local clock. The value must be calculated once per worker run and supplied to its queries, so a run cannot straddle the February boundary with two interpretations.

Eligibility data continues to be loaded for all years, because it is the source needed to identify registrations/status transitions. The rolling obligation-hydration worker operates **only** for `currentComplianceYear`, except for the explicit January/February handover below; it must not call the downstream organisation-obligation calculation endpoint for arbitrary historic or future years. Summary hydration is non-blocking: it enriches rows already eligible from the snapshot and declaration state.

This makes a full percentage-met response contract incompatible with historical unsubmitted requests: no fresh historic summary is maintained. The initial endpoint should therefore require its `obligationYear` to equal `currentComplianceYear` and return `400` for any other year. If historical unsubmitted lists are required later, they need their own explicit policy (for example a separate on-demand/export process, clearly non-current values, or additional historical hydration); they must not silently start the current-year poller for arbitrary years.

### January/February year handover

The current-year rule does **not** mean an abrupt stop at midnight. At the UK-time cutover from 31 January to 1 February, use a controlled dual-year handover:

| Period | Outgoing year (`Y - 1`) | Incoming year (`Y`) |
| --- | --- | --- |
| Before cutover | Continue normal rolling refreshes. | Best-effort pre-warming is allowed only from spare downstream capacity; it is not an availability requirement. |
| At cutover | It remains internally refreshed, but is no longer served by the current-year endpoint. | Becomes the endpoint's only permitted `obligationYear`; rows without a summary are returned with percentage met `0`. |
| After cutover grace | Stop scheduled refreshes; retain the final summaries under normal retention. | Continue normal rolling refreshes. |

This protects both sides of the boundary without making obligation hydration an availability dependency. An incoming-year organisation can be returned immediately when the endpoint changes year, with percentage met `0` until its first summary arrives. An outgoing-year PRN state change at, for example, 23:59 on 31 January must still receive a final organisation-obligation read after midnight; otherwise it could never be persisted locally.

Let `T` be the normal refresh interval and `H` the measured due-work/request completion allowance. Continue outgoing-year refreshes until at least `cutover + (T + H + J)` so the final pre-midnight change is captured. At the cutover, prioritise this outgoing-year catch-up over incoming-year hydration; incoming rows remain visible with the default percentage until normal capacity reaches them. During the brief overlap, the worker may work on both years, but it must honour its ordinary downstream rate limit.

For 500 organisations in both years, a 20-requests-per-minute cap lets the 500 outgoing catch-up reads complete in roughly 25 minutes. It cannot also make every incoming value current in that same window, but it does not need to: membership and display remain available with the default percentage. After the outgoing grace completes, return to normal one-year rolling work. This avoids a temporary rate increase to 40 requests per minute solely to pre-warm a non-blocking metric.

The implementation uses a configurable one-hour `OutgoingYearGracePeriod`: the 30-minute normal refresh interval, up to 25 minutes for the assumed 500-key catch-up at the shared 20-per-minute cap, and a five-minute allowance. During January it attempts incoming-year hydration only when the current-year batch has no due work. During the grace period it marks active outgoing summaries that have not had a successful post-cutover read as `Reconciliation` work, processes that year first, then uses remaining capacity for the new current year. Retries retain their back-off rather than being repeatedly requeued by reconciliation.

## Current frontend behaviour

The frontend route is `GET /certificates-of-compliance`. It has three tabs: `pending`, `accepted`, and `not-submitted`; each is separately selected for either `direct-producers` or `compliance-schemes`. `COMPLIANCE_YEAR` is currently configured as `2026`.

### Existing Waste Obligations declaration search

The frontend client calls Waste Obligations:

```text
GET /compliance-declarations
```

The endpoint (`SearchComplianceDeclarations`) supports `obligationYear`, comma-separated `status`, `registrationType`, `search`, `sort`, `page`, and `pageSize`. It queries the local `ComplianceDeclaration` Mongo collection, counts the result, applies a Mongo sort, then applies `skip` and `limit`.

The list tabs use it as follows:

| UI purpose | Example request |
| --- | --- |
| Pending page | `/compliance-declarations?obligationYear=2026&status=Submitted&registrationType=DirectProducer&page=1&pageSize=20&sort=DateSubmitted[desc],OrganisationName[asc]` |
| Accepted page | `/compliance-declarations?obligationYear=2026&status=Accepted&registrationType=DirectProducer&page=1&pageSize=20&sort=DateSubmitted[desc],OrganisationName[asc]` |
| Tab count | The same Submitted or Accepted request, with `pageSize=1`; the response `total` supplies the count. |
| Search box | `/compliance-declarations?obligationYear=2026&status=Submitted,Accepted&registrationType=DirectProducer&search={term}&page=1&pageSize=100&sort=DateSubmitted[desc],OrganisationName[asc]` |

`registrationType=DirectProducer` and `registrationType=ComplianceScheme` are the Waste Obligations declaration values. The existing declaration search is appropriate for Pending and Accepted because both have declaration documents. It cannot produce an unsubmitted row: no declaration document exists to query, and adding an `Unsubmitted` declaration status would be incorrect.

### Current Not submitted definition

For the selected year and review type, the frontend defines an organisation as not submitted when all of these are true:

1. Waste Organisations reports a matching **registered** registration.
2. There is no Waste Obligations declaration for the same organisation/year/type in either `Submitted` or `Accepted` status.

Consequently, an organisation that has only a `Cancelled` declaration remains in the Not submitted tab. This is covered by the frontend tests and must be preserved unless the business rule changes explicitly.

### Current Waste Organisations calls

The frontend calls the existing Waste Organisations search endpoint:

```text
GET /organisations
```

It always supplies `statuses=REGISTERED` and the compliance year. Its two type-specific variants are:

```text
GET /organisations?statuses=REGISTERED&registrations=SMALL_PRODUCER,LARGE_PRODUCER&registrationYears=2026
GET /organisations?statuses=REGISTERED&registrations=COMPLIANCE_SCHEME&registrationYears=2026
```

The API accepts comma-separated registration types, years, and statuses. It selects an organisation when **any** registration matches every supplied filter, returns an unpaged array, and returns the organisation's complete registration array. A local copy must therefore inspect registrations itself rather than assume every returned registration is an eligible one.

### Calls made for one page view

The summary is loaded for every tab as well as the active tab content. For a Not submitted page it performs the following work independently, so some calls are duplicated between summary and list construction:

```mermaid
sequenceDiagram
    participant UI as Regulator FE
    participant WO as Waste Organisations
    participant WOb as Waste Obligations
    participant Account as Account service
    participant PRN as WOb obligations route / PRN backend

    UI->>WO: GET /organisations (registered, type, year)
    UI->>WOb: GET /compliance-declarations (Submitted, all pages of 100)
    UI->>WOb: GET /compliance-declarations (Accepted, all pages of 100)
    Note over UI: subtract Submitted and Accepted organisation IDs
    Note over UI: sort whole candidate set, then choose page
    UI->>Account: POST batch reference lookup for page organisations
    loop Each visible direct-producer or scheme row
        UI->>PRN: GET /organisations/{id}/obligations?obligationYear=2026
    end
```

For direct producers the Account call is:

```text
POST /api/organisations/organisations-by-externalIds
{ "externalIds": ["{waste-organisations-id}", "..."] }
```

For compliance schemes it is:

```text
POST /api/organisations/organisations-by-companies-house-numbers
{ "companiesHouseNumbers": ["..."] }
```

The obligations call is currently made once per visible Not submitted row. The Waste Obligations route reads the organisation and calls `epr-prn-common-backend` for that organisation/year, then maps material-level obligations into its public DTO. It does **not** return percentage met. The frontend calculates percentage from the returned accepted and obligated tonnages. Waste Obligations does have an equivalent `ObligationCoveragePercentageCalculator`, but currently uses it when a submitted declaration is written (and in the corresponding migration), rather than in this read route. This is why percentage/recycling values are not safe to treat as static organisation data.

The page displays organisation name, organisation reference number, recycling obligations, and either Regulation 43 (schemes) or percentage met (direct producers). Not submitted rows have no declaration ID and no submission date. The frontend currently disables table sorting for this tab; it locally defaults to organisation-name order before page enrichment.

### CSV path

The CSV endpoint repeats the same data construction rather than exporting the displayed page.

- Pending and Accepted fetch every declaration page from `GET /compliance-declarations` and map the declaration rows.
- Not submitted repeats the full Waste Organisations search and full Submitted/Accepted declaration scans, then does one Account batch lookup and one obligations request per exported row (with bounded concurrency).
- The CSV does not retain the screen's selected sort. It exports the order produced by its independent fetch path.
- Pending and Accepted CSV calls currently omit `obligationYear`, unlike the on-screen list. That is a behavioural discrepancy to resolve before using CSV as a regression oracle.

## Why the local organisation copy is needed

Waste Obligations already has the declaration side of the anti-join locally. The missing left-hand side is the set of organisations eligible to submit for a given year and review type. Holding that set locally allows Mongo to page, count, and sort a stable candidate set without transferring every organisation and every matching declaration to the frontend on every request.

The snapshot is an eligibility projection, not a second system of record for organisations. It contains only fields required to establish eligibility and render/order an unsubmitted row.

## Organisation load options and source interface

`epr-prn-integration-function` is the upstream writer of Waste Organisations data from Synapse/Common Data. Its `UpdateWasteOrganisations` timer is currently configured as `1,31 0-7 * * *`: it starts every 30 minutes from 00:01 through 07:31 UTC and writes changed organisations individually. For each invocation, it reads the delta from its last successful cursor to the invocation's `utcNow`, writes the organisations one at a time, and advances that cursor only after all writes complete. Waste Obligations has no event or completion watermark from that flow and therefore cannot know that a particular upstream run is complete merely from the final timer tick.

There are three viable pull shapes:

| Option | Source calls | Advantages | Drawbacks |
| --- | --- | --- | --- |
| Per-year refresh | One search request with `registrationYears={year}` and no status/type filters for each requested obligation year. | Lower payload when only one/few years are served; simple year-scoped recovery. | Calls grow linearly with years; a cross-year backfill may expose some years from a newer source view than others. |
| Full all-years refresh | One unfiltered `GET /organisations`, then locally expand all registrations by year. | One source call; retains every returned registration status; each queryable year comes from the same source response. | Largest response and Mongo write volume on every poll. Must be load-tested against production cardinality. |
| Live source query on each review request | Call Waste Organisations for that request's year/type. | No local copy or refresh job. | Reintroduces frontend-style fan-out, cannot efficiently count/page/CSV with the declaration side, and couples the regulator view to source availability. Not recommended. |

The recommended first implementation is an **all-years full refresh at a configurable interval**, initially proposed as every 30 minutes. This keeps source calls bounded to one per completed poll interval without coupling Waste Obligations to a particular UTC clock time. If production payload size makes it unacceptable, fall back to per-year refresh with the same snapshot semantics.

### Proposed source call: no query parameters

The all-years ingestion client should not filter by type, status, or year:

```text
GET /organisations
```

No query parameters means the Waste Organisations search applies no registration filters and returns every organisation with at least one registration, including its complete registration array. This lets the local copy retain status transitions such as `REGISTERED` to `CANCELLED`, rather than discovering only that a formerly eligible organisation disappeared from a filtered result.

The response is transformed locally into at most two review rows per organisation/year:

| Source registration type | Review type stored locally |
| --- | --- |
| `LARGE_PRODUCER` | `DirectProducer` |
| `COMPLIANCE_SCHEME` | `ComplianceScheme` |

Waste Organisations enforces a unique registration key of `{ type, registrationYear }` within an organisation. Therefore each derived row represents exactly one current source registration, not a collection of registrations: an organisation with both relevant types in the same year has two local rows. Persist the current `registrationStatus`, including `CANCELLED`; filter `registrationStatus = REGISTERED` in the unsubmitted query. This retains the status needed to observe a Registered-to-Cancelled change without inventing an unnecessary local history collection.

`SMALL_PRODUCER` is intentionally not mapped to `DirectProducer`. The source integration confirms this: `epr-prn-integration-function` maps only source `DP`/`DR` to `LARGE_PRODUCER` and `CS` to `COMPLIANCE_SCHEME`; any other source type throws before it PUTs to Waste Organisations.

> **Current frontend defect:** the Direct Producer Waste Organisations query includes `SMALL_PRODUCER,LARGE_PRODUCER`. `SMALL_PRODUCER` is not part of the authoritative integration flow for this feature and must not be carried into the new Waste Obligations projection or endpoint. The frontend should be corrected when it moves to the new endpoint.

### Fetch everything; persist the data needed for the view

The recommended interpretation of “ingest everything” is to fetch every source organisation and registration, then retain all statuses of the two review-relevant types in the query projection. This is sufficient to identify a relevant organisation changing from Registered to Cancelled (or back again), while avoiding a second, uncontrolled copy of unrelated organisation data.

Persisting every source field and every unrelated registration type is a separate option. It provides a local raw-source archive for debugging or future features, but it adds storage, PII ownership, schema-versioning, and a further collection/projection to maintain. It is not required for the initial unsubmitted query. If that audit/archive requirement emerges, add it explicitly rather than allowing the review projection to become an accidental full replica of Waste Organisations.

The downstream client contract should be intentionally narrow and use a source-response model, for example:

```csharp
Task<WasteOrganisationsSearchResponse> SearchRegisteredComplianceOrganisations(
    CancellationToken cancellationToken);
```

This is an internal adapter contract, not a new public API. It must preserve source HTTP failures and cancellation rather than silently returning an empty population, because an empty population would be indistinguishable from every organisation having submitted.

## Data to persist

The query needs three local business projections. `ComplianceDeclaration` remains the authoritative declaration document, but the endpoint should not perform a full declaration anti-join for every page request when the same yes/no answer can be maintained at mutation time.

Names below are provisional. New collection names, indexes, schema files, migration scripts, and audit documentation must follow this repository's Mongo persistence process when implementation begins.

`activeGeneration` must be persisted in Mongo, not application memory or configuration, so every API instance reads the same active view after a restart or deployment. The recommended physical home is a dedicated `ComplianceEligibilitySnapshot` metadata collection containing one document for the all-years scope:

```json
{
  "_id": "all-years",
  "activeGeneration": "g1",
  "activeContentFingerprint": "sha256:...",
  "lastPromotedAt": "2026-08-26T08:15:00Z",
  "lastVerifiedAt": "2026-08-26T08:14:47Z",
  "sourceOrganisationCount": 12345,
  "resolvedReferenceRowCount": 12001,
  "excludedUnresolvedReferenceRowCount": 3,
  "status": "Ready"
}
```

The delivery adds purpose-specific persistence for eligibility rows, declaration-review state, snapshot metadata, the organisation-obligation summary, and its hydration work. The first three support the main inferred-list query; snapshot metadata is control data. The Account reference cache and the obligation-hydration work queue are internal work/provenance stores, not request-time joins. Each refresh or hydration worker uses its own private operational lease collection, accessed by its lease service rather than the query-data `IDbContext`.

### 1. Organisation eligibility snapshot

This is the refreshable Waste Organisations copy. Its job is to decide whether a row is eligible to submit and to provide the locally available row data.

```text
ComplianceEligibilitySnapshot
  scope                          AllYears | Year
  activeGeneration               GUID
  activeContentFingerprint       SHA-256 of the canonical derived eligibility set
  lastPromotedAt                 UTC timestamp of a changed snapshot becoming active
  lastVerifiedAt                 UTC timestamp of the latest successful source read, including no-change polls
  sourceOrganisationCount        int
  resolvedReferenceRowCount      int
  excludedUnresolvedReferenceRowCount int
  lastReferenceResolutionAt      UTC timestamp? of the latest resolved value materialised
  status                         Ready | Refreshing | Failed
  lastFailureAt / lastFailure     optional diagnostics

ComplianceEligibilityOrganisation
  generation                     GUID
  obligationYear                 int
  organisationId                 GUID
  reviewType                     DirectProducer | ComplianceScheme
  name                           string?
  tradingName                    string?
  companiesHouseNumber           string?
  registrationStatus             REGISTERED | CANCELLED
  referenceNumber                string?          // Account value; preserve leading zeroes
  referenceResolutionState       Resolved | Pending | NotFound | Ambiguous | AwaitingLookupKey | Failed
  sourceFingerprint              string           // Waste Organisations fields only
  materializedFingerprint        string           // source fields plus reference eligibility value
  refreshedAt                    UTC timestamp
```

The unique key is `{ generation, obligationYear, organisationId, reviewType }`. `generation` makes the active data set immutable during a refresh. `registrationStatus` is the source's single current status for that key; `REGISTERED` is necessary but not sufficient for eligibility. A row is considered by the unsubmitted query only when `registrationStatus = REGISTERED` **and** `referenceResolutionState = Resolved` with a non-empty `referenceNumber`. Other reference states remain stored for retry/diagnostics but are never a candidate. The UI display-name rule for schemes must be explicitly agreed before the endpoint uses `name` versus `tradingName`; the current frontend receives the full organisation DTO and its scheme-name handling should not be copied accidentally.

An empty result must never silently mean “every organisation has submitted” when source rows are being excluded for missing references. Persist the excluded count in snapshot metadata and emit it as an operational metric. The public endpoint deliberately returns only usable list data; a future administration/operational-insight endpoint will expose reference coverage, freshness, and other diagnostic state. The initial bootstrap should normally remain unavailable until its required reference coverage is reached; the policy for later newly-unresolved rows is an explicit open decision.

For this first delivery, sort raw `name` with `organisationId` as the required final tie-breaker. This deliberately follows the current `ComplianceDeclaration` approach: there is no shared search/sort projection, normalised sort key, or new schema migration for historic declarations. Raw Mongo string order is deterministic but is not an explicit case-insensitive or locale-aware alphabetisation contract; revisit that only if user-facing ordering requires it.

For an all-years refresh, `generation` is global to the refresh, rather than one generation per year. This ensures an endpoint for any year sees rows derived from the same upstream response. `ComplianceEligibilitySnapshot` is control metadata for the eligibility data set. The query uses three business projections: eligibility rows, declaration-review state, and the independently refreshed organisation-obligation summary. Reference-resolution records are an auxiliary work/provenance store, not a query-time join.

### 2. Unsubmitted-excluding declaration presence

This is a compact projection of the only declaration fact that the inferred state needs. It is independent of eligibility, so it can exist before an organisation first appears in a Waste Organisations snapshot.

```text
ComplianceDeclarationReviewState
  organisationId                 GUID
  obligationYear                 int
  registrationType               DirectProducer | ComplianceScheme
  unsubmittedExclusionCount      non-negative integer
  updatedAt                      UTC timestamp
```

The unique key is `{ organisationId, obligationYear, registrationType }`.

`unsubmittedExclusionCount` means the number of declarations that exclude an organisation from the inferred unsubmitted view. Today the set is `{ Submitted, Accepted }`, rather than every declaration. The use of a count rather than a boolean is important because the existing data model can contain more than one declaration for an organisation/year/type. A review row is unsubmitted exactly when this count is zero or no state row exists. Future statuses can join or leave the exclusion set without renaming the persisted field.

This preserves current frontend behaviour:

| Declaration state change | Exclusion count change | Is the organisation unsubmitted when no other qualifying declaration exists? |
| --- | ---: | --- |
| Create `Submitted` | `+1` | No |
| `Submitted` → `Accepted` | `0` | No |
| `Submitted` → `Cancelled` | `-1` | Yes |
| `Accepted` → `Cancelled` | `-1` | Yes |
| Delete `Submitted` or `Accepted` | `-1` | Yes |
| Create/delete/change `Cancelled` | `0` | Yes |

The create, status-update, and delete paths must update this projection in the **same Mongo transaction** as the declaration mutation and audit-event write. For a status update, calculate the delta from set membership before and after the transition; do not hard-code individual transitions. A count must never become negative: deployment needs a backfill from the existing `ComplianceDeclaration` collection before mutation code starts relying on it, and a periodic reconciliation should rebuild/compare the projection to detect drift.

### 3. Account organisation-reference resolution and materialisation

The Account service is authoritative for the organisation reference number used by the current frontend. It is not a uniform join:

| Review type | Account request | Join rule |
| --- | --- | --- |
| `DirectProducer` | `POST /api/organisations/organisations-by-externalIds` with `externalIds` | Waste Organisations `organisationId` is the Account `externalId`. The Account database has a unique `ExternalId` index. |
| `ComplianceScheme` | `POST /api/organisations/organisations-by-companies-house-numbers` with `companiesHouseNumbers` | A scheme's Waste Organisations ID is **not** an Account external ID. Match by Companies House number, then retain only the returned organisation whose `isComplianceScheme` is true. |

The Companies House route can return several Account organisations for one number. The frontend currently filters `isComplianceScheme` and puts the results into a JavaScript `Map`, so if two matching scheme rows were returned, the last response item would win accidentally. The cache worker must not copy that behaviour: zero matching scheme rows is unresolved and more than one is `Ambiguous`, alerted and withheld until the Account-data rule is resolved. It must never choose an arbitrary reference number. A missing Companies House number is `AwaitingLookupKey`, not a request to Account.

Reference numbers are strings, not numbers: preserve leading zeroes and the exact Account value. The expected six-digit format should be monitored, but it should not be silently coerced or truncated.

```text
OrganisationReferenceCache
  organisationId                 GUID
  reviewType                     DirectProducer | ComplianceScheme
  lookupMode                     AccountExternalId | CompaniesHouseNumber
  companiesHouseNumber           string?          // current source key for schemes
  referenceNumber                string?          // set only after a successful resolution
  resolutionState                Pending | Resolved | NotFound | Ambiguous | AwaitingLookupKey | Failed
  resolvedAccountExternalId      GUID?            // provenance, particularly useful for schemes
  resolvedUsingCompaniesHouseNumber string?       // provenance for scheme result
  firstSeenAt / lastSeenAt       UTC timestamp
  lastAttemptedAt                UTC timestamp?
  nextAttemptAt                  UTC timestamp?
  attemptCount                   int
  resolvedAt                     UTC timestamp?
  lastFailure                    optional, bounded diagnostic
```

The unique key is `{ organisationId, reviewType }`, not year or generation: a reference is assigned to the organisation, then reused by every current and future eligibility row. Add a due-work index such as `{ resolutionState, nextAttemptAt }`. This collection is an internal resolution queue/cache; the public unsubmitted query does not join it. The `resolvedUsing...` fields make the value explainable without turning the Account response into a second organisation master.

#### How it relates to generations

Materialise `referenceNumber` and `referenceResolutionState` into each staged `ComplianceEligibilityOrganisation` row and include their eligibility-relevant values in `materializedFingerprint` / `activeContentFingerprint`. This is preferable to a query-time join because a reference number is now a hard condition for appearing in the view and a generic-search field.

| Design | Strength | Cost / weakness | Decision under the no-reference rule |
| --- | --- | --- | --- |
| Separate reference cache joined by the API | Reference resolution can appear without rewriting a generation. | Generic search must join before count/page; a row with no reference still exists in the candidate population unless the query adds another exclusion. | Not selected. |
| Reference materialised in the staged generation | One local query serves filtering, reference search, page and CSV; promotion atomically publishes the reference-bearing eligibility set. | The first successful reference for an organisation causes a later complete generation write; resolution must be available before the row appears. | **Selected.** The resolution cache remains only an internal queue/provenance store. |

The required rule is deliberately stronger than “show `No data`”: an organisation with an unresolved reference is stored for provenance and retry, but its row is excluded by `referenceResolutionState != Resolved`. It is not considered unsubmitted, appears in no page/count/CSV, and cannot be found by reference search. Do not omit the source row from the staged generation entirely: retaining it with its resolution state makes retry, source-change detection, and later event hydration possible.

The initial `g1` flow is:

1. Fetch and transform the complete Waste Organisations response into staged source rows.
2. Seed/consult the reference-resolution cache for every distinct organisation/type and make bounded Account batch calls for cache misses.
3. Write every source row to `g1` with either a resolved reference or an unresolved state. Completion here means every Account batch has a recorded outcome; it does **not** mean every organisation has a reference.
4. Validate and atomically promote `g1`. Only its resolved-reference rows are eligible to appear.

For a later source refresh, compare each new row's **source** fingerprint to `g1`. A source-identical row copies its resolved reference or unresolved state forward; a changed row first reuses a resolved cache value for the same organisation/type, and only a cache miss needs an Account call. Thus a single genuinely new organisation normally creates one Account lookup, while all other references are copied into `g2`. Build the complete `g2` and promote it atomically only after the immediate lookup batch has outcomes.

An Account timeout, a not-found result, or an ambiguous scheme must not indefinitely hold an otherwise valid Waste Organisations snapshot hostage. Record `Failed`, `NotFound`, or `Ambiguous`, write that unresolved state into the staged generation, and promote the generation. The row is excluded, satisfying the no-reference rule, while the retry worker continues. When a retry later becomes `Resolved`, coalesce the changed resolutions and materialise a fresh complete generation on the next refresh cycle (or a deliberately scheduled materialisation cycle); do not mutate active generation rows in place.

This is safe because the stated business invariant is that a reference number never changes once assigned to an organisation ID. If Account ever returns a different non-empty reference for a `Resolved` cache key, retain the first value, record an integrity error, and investigate; do not silently overwrite it. For a compliance scheme, a change to the source Companies House number after resolution is likewise an integrity signal, not a reason to substitute a new reference automatically. An unresolved scheme may update its lookup key from the next source generation and become due again.

#### Reference-resolution worker

Run a separate interval worker using the existing `AuditEventLeaseService` lifecycle: atomic acquire-or-skip, renewal while a batch is being processed, owner-only release, and expiry recovery. It needs its own private operational lease collection and a distinct lease ID, for example `organisation-reference-resolution`. This lets it run independently of the 30-minute Waste Organisations poll and prevents multiple hosts from sending the same batch to Account.

Each run selects a bounded number of due `Pending`, retryable `NotFound`, or `Failed` cache entries and groups them by lookup mode:

1. Deduplicate Direct Producer organisation IDs and call the external-ID batch endpoint in configured chunks.
2. Deduplicate scheme Companies House numbers and call the Companies House batch endpoint in configured chunks; one response may resolve several cache entries with the same number.
3. Write only successful non-empty reference numbers as `Resolved`. `notFoundExternalIds`, a missing reference number, a source key not yet present, ambiguous scheme results, and transient HTTP failures each retain an unresolved state with an appropriate retry/back-off policy.
4. Renew the lease before each downstream call and before writing its result. If renewal fails, stop without claiming the remaining work; it will be picked up after lease expiry.

The Account endpoints are already batch interfaces, but the service has no published request-size contract in the code inspected. Use a conservative configurable chunk size and low configurable concurrency, load-test it with the Account team, then set an explicit contract/limit rather than sending the full initial population in one request. A positive cache entry is never polled again. Negative outcomes are not terminal: an organisation can exist before a reference is assigned, so retry them with capped exponential back-off and a much lower steady-state cadence. Track pending count, oldest unresolved age, retry/failure count, ambiguous schemes, and the number of new cache keys discovered per generation.

#### Serving the materialised unsubmitted view without HTTP fan-out

No change is proposed to `GET /compliance-declarations`: its existing generic `search` already includes its persisted `organisation.referenceNumber`. This Account cache and its search logic exist only for the new unsubmitted projection.

The unsubmitted query reads only the active generation and needs no Account call and no reference-cache join. Reference numbers are already on eligible rows, so both the normal page/CSV and generic search are one local Mongo query. The unsubmitted endpoint uses the same generic `search` parameter and case-insensitive partial-match semantics as the existing declaration endpoint. Its aggregation becomes:

```text
1. Match active generation + obligation year + review type + registrationStatus=REGISTERED
   + referenceResolutionState=Resolved.
2. Match escaped, case-insensitive contains regex over name OR tradingName OR referenceNumber.
3. Lookup ComplianceDeclarationReviewState and retain a zero/absent count.
4. Sort, count, and page.
5. Local-batch lookup `OrganisationObligationSummary` for page rows; map a missing or incomplete summary to the documented zero/default metric state.
```

This is entirely local Mongo work. The same unanchored contains limitation remains, but no per-candidate join is required. During the day-one Account backfill, an organisation with a pending reference is excluded from the view altogether; it cannot be found by name or reference until a later promoted generation contains its resolved reference. Monitoring must expose the excluded unresolved-row count and oldest pending age. There is deliberately no request-time fallback to Account: that would make view membership depend on downstream availability and reintroduce HTTP calls into search.

Do not enable `OrganisationReferenceNumber` sorting in this phase merely because it is now materialised. It would be technically possible with a dedicated index and an agreed order/null contract, but no current UI requires it.

Suggested indexes, subject to the repository's Mongo schema/migration process:

- eligibility rows: `{ generation, obligationYear, reviewType, registrationStatus, referenceResolutionState, name, organisationId }` for eligibility filtering and deterministic default ordering;
- eligibility rows: unique `{ generation, obligationYear, organisationId, reviewType }`;
- review state: unique `{ organisationId, obligationYear, registrationType }`;
- reference cache: unique `{ organisationId, reviewType }` and due-work `{ resolutionState, nextAttemptAt }`;
- obligation summary: unique `{ organisationId, obligationYear }`; and
- obligation hydration work: unique `{ organisationId, obligationYear }` plus due-work `{ nextAttemptAt, priority }`.

No obligation calculation, current-obligation value, Regulation 43, or inferred `unsubmitted` boolean belongs in the eligibility projection. The Account reference is deliberately materialised there; its separate resolution cache remains internal work/provenance data. Organisation-obligation values are stored in the distinct projection below because a PRN's status can change them after the organisation generation has been promoted.

### 4. Organisation-obligation-summary hydration

Percentage met is required in the Direct Producer unsubmitted view, and recycling-obligations status is required for both review types. They must be local values: the current frontend makes one obligations request for each visible row and repeats the fan-out for every CSV row. That cannot be retained by a server-side endpoint intended to page, count, search, and export efficiently.

#### What the downstream calculation currently represents

In this section, **PRN backend** is the name of the downstream service and **PRN** means an individual evidence record. The worker does not request or cache individual PRNs. It requests an organisation's current obligation calculation for one year and stores only the derived organisation-obligation summary.

Waste Obligations already calls the PRN backend directly through `IPrnCommonBackendService.ReadObligations(organisationId, year)`. That makes:

```text
GET api/v1/prn/obligationcalculation/{year}
X-EPR-ORGANISATION: {organisationId}
```

The current public Waste Obligations route, `GET /organisations/{organisationId}/obligations?obligationYear={year}`, first validates the organisation in Waste Organisations and, in parallel, makes the above current-obligation calculation call. A background hydrator already has a valid local eligibility row, so it must call `IPrnCommonBackendService` directly, **not** call the public Waste Obligations route. That avoids an unnecessary Waste Organisations HTTP request per hydrated row and avoids routing internal work back through the API surface.

The PRN backend's calculation endpoint combines two inputs:

- stored `ObligationCalculations` for the organisation/year, which are recalculated once per day by its separate obligation-calculation process; and
- a live aggregate of the organisation's `ACCEPTED` and `AWAITINGACCEPTANCE` PRN records for that year.

Consequently, a state change to an individual PRN is the **only real-time input** that can change an organisation's returned obligations. It can alter accepted tonnage and percentage without a new Waste Organisations generation or daily calculation run. The obligation calculation itself is the other input, but changes only at its daily recomputation. The initial model does not track either input directly. Instead, it periodically re-reads each current-year organisation's calculated obligations. One rolling refresh mechanism therefore covers both kinds of change without polling individual PRNs. A successful local read is an observation of those two inputs, not immutable organisation data.

The new worker must calculate the display value in Waste Obligations, using the existing `ObligationCoveragePercentageCalculator`: sum the mapped material `accepted` and `obligated` tonnages, calculate `accepted / obligated * 100`, cap at 100, and round to a whole number away from zero. Extract/reuse this as a tested mapper or calculator method accepting the PRN response model, rather than copy the JavaScript frontend calculation. On an empty successful response, preserve today's Not submitted behaviour: `recyclingObligationsMet` is `null` and percentage met is `0`.

#### Projection and work queue

Add a per-organisation/year summary; `reviewType` is intentionally not part of its key because the PRN backend's organisation-obligation calculation endpoint is keyed only by organisation and obligation year. This lets one summary be reused if an organisation is represented by more than one review row.

```text
OrganisationObligationSummary
  organisationId                 GUID
  obligationYear                 int
  obligationCount                int             // zero is a successful, empty result
  totalAcceptedTonnage           int
  totalObligatedTonnage          int
  recyclingObligationsMet        bool?           // exact current frontend/domain semantics
  obligationCoveragePercentage   decimal?        // whole number; 0 for a successful empty result
  sourceFingerprint              string           // canonical mapped organisation-obligation result, for no-op writes/telemetry
  lastSuccessfulReadAt           UTC timestamp   // when Waste Obligations observed the calculation endpoint
  dailyCalculationRunId          string?          // populated when the source later supplies a run-completion watermark
  lastAttemptedAt                UTC timestamp
  nextRefreshAt                  UTC timestamp
  refreshState                   Ready | Pending | Failed
  attemptCount                   int
  lastFailure                    optional, bounded diagnostic

OrganisationObligationHydrationWork
  organisationId + obligationYear                // unique work key
  priority                       NewEligible | ScheduledRefresh | Retry | Reconciliation
  nextAttemptAt, attemptCount, lastFailure
  requestedAt, lastSuccessfulReadAt
```

Use a unique index on `{ organisationId, obligationYear }` for both collections, plus a due-work index on `{ nextAttemptAt, priority }`. The work record may instead be folded into the summary document if the repository's persistence conventions favour one collection; keeping it separate makes a `Ready` read model small and lets queue retries be retained without complicating query indexes. Neither is joined to Account or any remote service at request time.

Persisting material-level obligations is not required for this list. The totals, status, percentage, source fingerprint, and timestamps are enough to render today's columns and diagnose the calculation. If a later API must return a material breakdown, add a deliberately versioned nested snapshot then; do not turn this list projection into an unbounded PRN archive.

#### Hydration lifecycle

The obligation hydrator is a second interval worker using the existing `AuditEventLeaseService` lifecycle, with its own private operational lease collection and lease ID such as `organisation-obligation-hydration`. Its lease is independent of the organisation-refresh and Account-reference leases. It acquires-or-skips, renews before/while a bounded batch is processed, and writes an atomic upsert of the summary and work outcome. A failed lease renewal cancels the remainder of that batch; another host can resume it after expiry.

On a changed organisation generation, restrict all obligation work to `obligationYear = currentComplianceYear`:

1. Identify active rows for the current compliance year that are `REGISTERED`, have a resolved reference, and have no current-enough `{ organisationId, obligationYear }` summary. Deduplicate to work keys and enqueue them with `NewEligible` priority.
   Before selecting due work, remove current-year work keys that no longer satisfy those active-generation conditions. This prevents a cancellation or an unresolved reference in a later generation from continuing to generate PRN calculation calls.
2. Reuse an existing summary for source-identical rows until its `nextRefreshAt` is due. A reference becoming resolved can enqueue the existing organisation/year without changing the PRN key.
3. The obligation worker selects a bounded due batch, calls `IPrnCommonBackendService.ReadObligations` for each key with deliberately low, configurable concurrency, maps the result, calculates the summary, and upserts it. A result with an unchanged `sourceFingerprint` updates freshness timestamps but need not rewrite the metric fields.
4. A transient PRN failure records `Failed` and uses capped exponential back-off. It does not alter declaration presence or organisation eligibility. A successful empty response is `Ready`, not a failure.
5. Schedule every `Ready` current-year summary for its next read at `lastSuccessfulReadAt + RefreshInterval`. Do **not** wait for, or poll for, a PRN-state signal. A change in either source input is picked up at that organisation's next scheduled read.
6. Spread the due times deterministically over each interval, for example by using a stable hash of `{ organisationId, obligationYear }` as a slot within the interval. For the initial assumption of `K = 500` and a 30-minute interval this makes about 17 calls due each minute, rather than a 500-call burst at one clock time. The worker claims small due batches under its lease and uses the singleton `OrganisationObligationRequestPacer` to reserve one evenly spaced downstream slot at the shared 20-requests-per-minute limit. Low concurrency (initially two requests) separately bounds in-flight calls. New-organisation reads and retries pass through the same pacer; the limit cannot be bypassed by a separate retry path. A full batch starts the next batch without the normal idle wake delay, so pacing remains capable of 20 requests per minute.
7. Do **not** poll individual PRNs or poll a PRN-change feed in this implementation. There is no suitable PRN-state trigger today, and such polling would create unacceptable volume. A PRN state change and a daily `ObligationCalculation` change are both reflected no later than the next rolling organisation-obligation read, subject to retry/failure handling.
8. Retain a low-frequency full current-year `Reconciliation` sweep only as repair for failed work or projection drift. It is not an additional near-real-time polling mechanism.

At the 1 February UK-time boundary, the worker applies the dual-year handover described above: it has already pre-warmed the new current year, continues the previous year for its post-cutover grace, then stops scheduled work for that outgoing year. Previous-year summaries may be retained under a normal operational retention policy for diagnostics, but cannot make the current-year endpoint serve a historical request.

**Side requirement — hydration-work retention.** The unique `{ organisationId, obligationYear }` work key prevents repeated polling and retries from creating duplicate work: there is one work record per organisation/year, and two years are active only during handover. The current implementation stops processing the outgoing year's work after the grace period but does not yet remove those old work records. Before long-term historical operation, add a bounded cleanup or expiry policy for obsolete hydration work. Retention of previous-year summaries is a separate diagnostic-data decision.

Do not wait for this work as part of an organisation-generation promotion. The reference is a stable identity value and a hard membership condition; the current-obligation percentage is a volatile display metric. If a PRN status change rewrote the complete eligibility generation, it would cost `O(M)` eligibility writes and repeatedly invalidate otherwise unchanged organisation data. The selected split instead costs one organisation-obligation calculation read and one summary upsert per affected organisation/year, while the organisation generation remains unchanged.

#### Non-blocking obligation enrichment

Organisation-obligation hydration is not an eligibility or endpoint-availability condition. A candidate belongs in the view solely because it has a registered eligibility row, a resolved reference number, and no Submitted/Accepted declaration. The endpoint must behave as follows:

- no `OrganisationObligationSummary` yet: return `obligationCoveragePercentage: 0`, `recyclingObligationsMet: null`, `obligationDataState: "Pending"`, and `obligationsAsOf: null`;
- current `Ready` summary: return its calculated percentage/status and its `lastSuccessfulReadAt`;
- failed or stale summary: return its most recently calculated percentage/status when one exists, otherwise the same `0`/`null` default, together with `obligationDataState: "Failed"` or `"Stale"` for observability.

The frontend can initially display the percentage as `0%`, as required. `obligationDataState` is additive metadata for monitoring or a later UI improvement; it does not change the displayed membership or require a warning in the first delivery. The worker's freshness window produces alerts and retry work, not `503` responses. The endpoint still fails closed for a stale **eligibility** snapshot, because that can make organisations disappear or appear incorrectly; a missing obligation summary cannot.

#### Empty-system bootstrap example: approximately 500 organisations

Assume an initially empty Waste Obligations deployment and `K = 500` distinct active organisation keys for the **current compliance year** after the source response is expanded. Assume each has one relevant registration and every required Account reference can be resolved. An organisation may have review rows for other years in the eligibility generation, but those rows do not create current-obligation work or downstream calls.

| Stage | External requests | Volume for this example | When it happens |
| --- | --- | ---: | --- |
| Organisation snapshot | `GET /organisations` to Waste Organisations, with no query parameters | **1** request | At the first acquired eligibility-refresh lease, after bounded startup jitter. |
| Reference resolution — Direct Producers | Account external-ID batch endpoint | `ceil(D / B_d)` requests, where `D` is the number of direct-producer keys and `B_d` is the agreed batch size. | Immediately after source transformation, before `g1` is promoted. |
| Reference resolution — Compliance Schemes | Account Companies-House-number batch endpoint | `ceil(H / B_s)` requests, where `H` is the number of distinct scheme Companies House numbers and `B_s` is its agreed batch size. | In the same initial resolution phase. |
| Declaration state | Mongo backfill of `ComplianceDeclarationReviewState` | **0** external HTTP requests | Before the endpoint is enabled; an empty declaration collection produces zero state rows. |
| Obligation hydration | Direct organisation-obligation calculation call through `IPrnCommonBackendService` | **500** requests: one for each distinct organisation/year key. | Immediately after `g1` is promoted and its work has been enqueued. |

For illustration only, if Account accepts batches of 100 and all 500 are Direct Producers, the Account step is five requests. If 250 are Direct Producers and 250 are schemes with distinct Companies House numbers, it is three requests to each Account endpoint, six in total. The actual Account batch limit is not currently a published contract and must be agreed; it is deliberately configurable. Scheme lookups are deduplicated by Companies House number, so shared numbers reduce the request count.

The initial external-request total is therefore:

```text
1 Waste Organisations request
+ ceil(D / B_d) Account external-ID requests
+ ceil(H / B_s) Account Companies House requests
+ K organisation-obligation calculation requests
```

There is no initial scan of `GET /compliance-declarations`, no request to `epr-prn-integration-function`, and no request-time Account/current-obligation call from the unsubmitted endpoint. Declaration state is already local to Waste Obligations and is backfilled from Mongo.

`g1` can be promoted once source rows and Account outcomes are recorded. `GET /compliance-declarations/unsubmitted` can then return its eligible unsubmitted organisations immediately, without waiting for the 500 organisation-obligation calculation calls. Until an individual summary is hydrated, that row returns percentage met `0`, recycling-obligations status `null`, and `obligationDataState: "Pending"`. It is still entirely served from local Mongo.

The organisation-obligation calculation endpoint has no batch operation. With the initial shared cap of 20 requests per minute, the 500-key bootstrap takes at least 25 minutes, before downstream latency, timeout, throttling, and Mongo-upsert time are considered. Low concurrency bounds short bursts; the paced rate limit bounds the aggregate request volume. Do not publish a bootstrap time SLA before production-like load testing.

The initial implementation does not directly signal the hydration worker when `g1` is promoted. New keys are due immediately and are discovered on the next ordinary worker wake-up, adding at most that wake interval before the bootstrap drain begins. This is an accepted non-blocking edge case: it does not affect organisation membership or endpoint availability, and a future direct signal may reduce bootstrap latency if measurements show that it matters.

The recommended initial `RefreshInterval` is **30 minutes**. It is a balanced starting point for roughly 500 organisations: every organisation's current obligations are at most about 30 minutes plus queue/retry delay behind either a PRN state change or the daily obligation-calculation update, while the normal downstream rate stays low and even.

| Refresh interval | Reads for 500 current-year organisations | Average downstream rate | Approximate daily volume | Assessment |
| --- | ---: | ---: | ---: | --- |
| 15 minutes | 500 every 15 minutes | 33/minute (0.56 req/s) | 48,000/day | Fresher, but doubles avoidable volume before there is a change signal. |
| **30 minutes** | **500 every 30 minutes** | **17/minute (0.28 req/s)** | **24,000/day** | **Recommended initial window.** |
| 60 minutes | 500 every 60 minutes | 8–9/minute (0.14 req/s) | 12,000/day | Lower load, but too stale for a user-driven accept/reject outcome. |

Use a short worker wake-up/lease-attempt interval (for example one minute) only to claim due, new-organisation, or retry work. It does not cause downstream reads when no work is due. For the recommended interval, spread the 500 calls across the 30-minute window, process at low configured concurrency (initially two), and enforce a shared, paced 20-requests-per-minute client-side rate limit. This is a controlled average of 17 calls per minute, not a 500-call burst. The cap leaves about 3 requests per minute of normal headroom for retries and newly eligible organisations. Confirm the downstream behaviour with a production-like load test and the PRN backend owners.

A new eligible organisation becomes due for one calculation read immediately and is picked up on the next worker wake-up. A source-only change such as a name change reuses the current summary and adds none. A declaration submission/cancellation updates local declaration state immediately and adds none. The trade-off is explicit: this does **not** make PRN status live; a status change is normally visible at the organisation's next 30-minute read. If a future durable PRN-state event becomes available, it can enqueue one affected organisation/year calculation read, but it is not part of this initial design and does not justify polling for PRN changes now.

#### Future consideration: weighted refresh frequency

The initial policy intentionally gives every current-year organisation the same 30-minute target. If the active population grows beyond the assumed 500 keys, do not increase the global request cap or silently lengthen every organisation's refresh interval without an explicit capacity and business decision. For example, 1,000 keys require an average 33 requests per minute and 2,000 keys require 67 requests per minute to retain a 30-minute target; at the initial 20-per-minute cap they cannot do so.

A future policy may assign a longer normal refresh interval to lower-impact organisations while retaining a global cap. It should be based on agreed local inputs, such as the registration type and the annual obligated tonnage from the last successful summary, rather than a guess based on organisation type alone. The business must define the bucket thresholds, maximum staleness per bucket, bootstrap treatment before a first successful read, reclassification rules, fairness/starvation guarantees, and how the policy is exposed in monitoring. Until that work is agreed, every organisation uses the same interval and a state-changing retry continues to use the shared capped quota.

#### Serving, CSV, and future metric sorting

For the initial name-sorted endpoint, apply the eligibility/declaration anti-join, count/page it, then local-lookup `OrganisationObligationSummary` for the selected page. The CSV streams the same local summaries in bounded Mongo batches. Neither path calls the PRN backend. A page can deliberately mix `Ready`, `Pending`, `Stale`, and `Failed` metric states: each row maps its own local summary or zero/default values, while eligibility remains consistent from one active generation.

Percentage met and recycling-obligations status are **not** in the first `sort` allow-list. A Mongo `$lookup` to a separate summary before globally sorting can serve a correct result, but it requires considering every unsubmitted candidate and cannot normally use the summary's percentage index to make the joined sort cheap. Enriching only an already paged name sort is never valid for a percentage sort.

If product later requires server-side percentage sorting, choose one of these explicit designs:

| Design | Correctness and cost | Recommendation |
| --- | --- | --- |
| Aggregate lookup then sort all candidates | Correct, but performs a summary lookup and sort over the entire inferred population for every request/export. | Accept only after production-cardinality measurement and if the population is small. |
| Denormalise the metric into a final `UnsubmittedOrganisationProjection` row | The summary worker updates the one matching active organisation/year/type row when the PRN result changes; a compound index can then support percentage ordering. Requires per-row versioning and a clear writer/consistency rule. | Preferred if percentage sorting becomes a real requirement. |
| Call PRN after pagination | Cheap only for display. It produces incorrect global order and CSV disagreement. | Never use for a sortable field. |

The first endpoint can therefore return `recyclingObligationsMet`, `obligationCoveragePercentage`, and `obligationsAsOf` without expanding the agreed `sort` set. Regulation 43 remains `null` for an unsubmitted scheme because it is declaration content, not PRN content.

## Refresh design

Waste Organisations offers neither paging nor a change cursor for this query. Therefore the initial implementation is a full, bounded poll. It should be a `BackgroundService`, following the existing hosted-service pattern, with a Mongo-distributed lease so multiple API instances do not refresh concurrently.

### Cross-host refresh lease

Follow the existing recurring-worker lease lifecycle in `AuditEventLeaseService` / `AnalyticsAuditEventProcessor`, rather than the startup-only `MongoMigrationService` pattern. The audit-event lease has the required lifecycle for deployed multi-host workers: an instance-specific owner ID, expiry, atomic acquire-or-skip, renewal while processing, and owner-only release.

The organisation worker owns its private operational collection directly through its lease service; it must not reuse the audit-dispatch process name, couple the refresh to audit-event data, or add the lease collection to the query-data `IDbContext`. Values:

```text
leaseId:      organisation-eligibility-refresh
collection:   _organisation_eligibility_refresh_lease
document _id: organisation-eligibility-refresh
```

The lease document follows the existing shape:

```json
{
  "_id": "organisation-eligibility-refresh",
  "owner": "{machine-name}-{instance-guid}",
  "expiresAt": "2026-08-26T08:20:00Z",
  "createdAt": "2026-08-26T08:15:00Z",
  "updatedAt": "2026-08-26T08:15:00Z",
  "lastReleasedAt": null
}
```

Each host uses the same configurable `PollingIntervalMinutes`; it is not a cron schedule. On startup, each host waits a small bounded random delay, then attempts a run. Subsequent attempts are targeted at a fixed interval from the previous attempt's start; an instance never overlaps its own runs. `PollingIntervalMinutes` must therefore be greater than the measured maximum normal refresh duration—if a run overruns, the next attempt occurs once it finishes and the freshness bound is breached/alerted. Every attempt calls `TryAcquire`. Mongo atomically grants the lease only when it has expired or is already owned by that same instance. Hosts that do not acquire it log/measure the skip and return; they do not wait or run a duplicate poll.

The owner must renew the lease throughout the source request, transformation, and every bounded bulk-write sequence. If renewal fails, it must cancel the refresh and **must not promote** the staged generation. Immediately before the metadata pointer is changed, renew/check ownership again. Release in `finally` by unsetting the owner and expiring the lease, as the audit-event worker does. If a host crashes, another host can acquire the lease after expiry and safely create its own staged generation.

Lease duration and renewal cadence are configuration. The duration should exceed a normal bulk-write batch, not the full expected refresh duration; renewal provides safe ownership for longer runs. Record the same operational outcomes as the existing pattern: acquired, not acquired, renewal failed, released, and processing duration.

The periodic poll intentionally is not aligned to the upstream cron. A poll can occur while the integration function is still writing, but the next poll produces a complete new local generation from the then-current Waste Organisations view. This makes deployments and active multi-host instances simple: all hosts run the same interval loop and the lease selects one. It does not turn the upstream schedule into a completion SLA. If there are manual Waste Organisations changes outside the integration function, they are visible to the next successful poll rather than waiting for the overnight integration window.

One refresh run should work as follows:

1. Acquire an `eligibility-organisations` lease. If another instance holds it, skip this interval.
2. Fetch the single combined Waste Organisations search response with a timeout and normal HTTP resilience policy.
3. Validate the response, then expand every relevant source registration into one `{ organisationId, obligationYear, reviewType }` review row. Retain its current status for `LARGE_PRODUCER` and `COMPLIANCE_SCHEME`, including non-`REGISTERED` statuses; ignore unrelated registration types in the derived projection.
4. For every derived row, calculate its Waste Organisations-only `sourceFingerprint`. Compare it with the same-key row in the active generation; copy any known reference state forward or obtain it from the resolution cache. Create `Pending` work and make bounded immediate Account batch calls only for cache misses.
5. Materialise the Account outcome on every staged row. A successful reference produces `Resolved` plus its string value; every other outcome produces an excluded unresolved state. Record source/row/duplicate/reference-outcome counts for diagnostics.
6. Canonically sort the complete materialised set and calculate its semantic `activeContentFingerprint`. The fingerprint includes each row's `sourceFingerprint` and either its resolved reference value or one common `Unresolved` marker; it excludes retry timestamps and distinctions between non-eligible states such as `Pending` and `Failed`.
7. Compare that fingerprint with the active generation in metadata.
8. If the fingerprint is unchanged, atomically update only metadata such as `lastVerifiedAt`, source/row counts, and the no-change outcome. Do **not** create a generation, write eligibility rows, or alter the active pointer.
9. If the fingerprint changed, bulk-write individual materialised eligibility documents under a new generation. The write key is `{ generation, obligationYear, organisationId, reviewType }`; do **not** modify the active generation in place.
10. Verify the intended row count was written. Only after every bulk write succeeds, atomically switch snapshot metadata to the new generation, fingerprint, and `lastPromotedAt`/`lastVerifiedAt`.
11. Delete old generations asynchronously after a retention period. A failed run never changes the active generation.

This gives readers a complete old snapshot or a complete new one—never a partially refreshed population. It also avoids a failed source call being interpreted as no organisations.

The run needs a data-quality guard before fingerprint comparison or promotion. At minimum it should detect invalid required IDs/years, duplicate keys after transformation, and a sudden source/row-count collapse against the last successful run. The policy for a legitimate zero-result snapshot must be agreed; it should not silently replace a non-empty active population.

### Minimising data churn on frequent polls

With the original generation-only description, every 30-minute poll would create a full new set of eligibility documents, even when Waste Organisations had not changed. That is safe but unnecessarily expensive. The poller should instead use **copy on semantic change**. The full-set fingerprint comparison happens **before** any staged-generation write:

1. It must still call unfiltered `GET /organisations`. Waste Organisations exposes neither a change cursor nor a conditional/ETag contract in the current interface, so there is no safe way to know that the source is unchanged before reading it.
2. It transforms the response into source rows, reuses/calls Account resolution as described above, then forms precisely the materialised eligibility fields that would be persisted. It sorts rows by `{ organisationId, obligationYear, reviewType }`, sorts each matching-registration list deterministically, serialises them canonically, and hashes that value (for example SHA-256). Do not include retrieval timestamps, response ordering, transport-only fields, or non-eligible retry-state changes in the hash.
3. If that full-set hash equals `activeContentFingerprint`, the poll is successful but is a **no-change** outcome. Update the small metadata document's `lastVerifiedAt` and metrics only. Existing eligibility rows and their generation remain untouched.
4. If the hash differs—because source data changed or an unresolved reference became resolved—create and validate `g(n+1)`, then promote it as described below. The old data remains active until that promotion.

The fingerprint deliberately covers the **derived materialised projection**, not the raw source response. A source change to an ignored registration type or an unpersisted field therefore does not create a new generation. An Account retry changing `Pending` to `Failed` also does not create one because neither value makes the row eligible; the first resolved reference does. This eliminates Mongo eligibility-row churn entirely when there are no relevant changes. It also means the endpoint can truthfully report that the source population was recently checked even when the data generation was last promoted days earlier. `lastVerifiedAt` is the freshness value for the endpoint; `lastPromotedAt` is the last time queryable data actually changed.

When only a few organisations change—or a small number of references become resolved—the first implementation should still write a complete new generation. It performs one full source GET and one full local generation write *only on a semantic materialised-view change*, preserving the simple, indexed query that selects one generation. The per-row `sourceFingerprint` remains useful for diagnostics and later optimisation, but must not cause an in-place partial update of the active snapshot.

#### Fingerprint algorithm and cost

The fingerprint is cheap relative to an HTTP download and a full Mongo generation write, but it is not free: Waste Obligations must inspect every relevant source registration because the current source has no change token. Let `N` be the number of organisations, `R` the number of relevant `LARGE_PRODUCER`/`COMPLIANCE_SCHEME` registrations, and `M` the derived `{ organisationId, obligationYear, reviewType }` rows.

1. Read each source organisation and retain only the fields that would be stored in the eligibility projection. Convert each relevant source registration into its derived row and validate the source's `{ organisationId, type, registrationYear }` uniqueness.
2. For each row, calculate `sourceFingerprint`: SHA-256 over a versioned, length-prefixed canonical encoding of its key and Waste Organisations fields. Resolve/copy its Account value, then calculate `materializedFingerprint` over the source fingerprint plus either the exact resolved reference string or the common `Unresolved` marker. Nulls, enum values, dates (UTC ISO-8601), and strings must each have one defined representation.
3. Sort the small row descriptors by the composite row key using ordinal comparisons. Do not rely on Waste Organisations response order, which is not a source contract.
4. Incrementally calculate `activeContentFingerprint` as SHA-256 over a version tag, the row count, and the ordered `{ row key, materializedFingerprint }` pairs. Persist and compare the resulting 32-byte digest and row count in snapshot metadata.

Hashing row fingerprints rather than serialising a second giant JSON document avoids a large temporary string/byte array and makes the source/materialised fingerprints reusable for diagnostics. It also means a source response that merely changes array ordering produces the same fingerprint.

| Work | Cost per poll | Notes |
| --- | --- | --- |
| Download and JSON parse | `O(source response bytes)` | Unavoidable with the current all-organisations endpoint. |
| Transform and row hashing | `O(R + M)` | One pass over relevant registrations/derived rows. |
| Reference cache lookup / new-key batches | `O(M)` local lookups; Account calls scale with cache misses | Existing references are copied forward. Batch and bound the initial / genuinely new-key Account work. |
| Deterministic ordering | `O(M log M)` time, `O(M)` row-descriptor memory | Necessary because source ordering is not guaranteed. |
| No-change Mongo work | `O(1)` eligibility writes | A metadata update plus the normal lease operations; no eligibility rows are touched. |
| Changed Mongo work | `O(M)` eligibility writes | A complete `g(n+1)` is written to retain atomic simple-snapshot semantics. |

In practical terms, the no-change path trades one full source download, parse, transform, and in-memory sort for avoiding `M` Mongo document writes, their index updates, generation retention, and later cleanup. At normal population sizes, source I/O and Mongo writes should dominate SHA-256 CPU; profile with production-like data rather than assume this. If the payload makes memory material, deserialize the HTTP response as a JSON stream and retain only the `M` compact row descriptors needed for sorting/fingerprinting, rather than retaining a second full source object graph.

SHA-256 is a content fingerprint, not a mathematical proof of equality. Prefixing an algorithm/schema version and row count prevents accidental incompatibility as the projection changes; the probability of an accidental collision is negligible for this operational use. If that residual risk is not acceptable, the no-change path must instead perform an exact canonical row-by-row comparison with the active generation, adding a full Mongo read and substantially reducing the benefit.

#### Mongo change-identification query

The normal no-change decision does **not** use a Mongo row-comparison query and does not write `g2`: it reads the one snapshot-metadata document and compares the complete-set fingerprint. That is the efficient path when nothing changed. A full `g2` is written only after a fingerprint mismatch has established that the derived set differs.

If a changed materialised result has been written to staged `g2`, an optional background aggregation can classify the difference from active `g1` for metrics, audit diagnostics, or a later decision to introduce sparse overlays. It must never run in the API request path. Project both `sourceFingerprint` and `materializedFingerprint`: a changed source fingerprint is an upstream organisation change; an unchanged source fingerprint with a changed materialized fingerprint is an Account-reference resolution change. The forward pass identifies added and changed rows:

```javascript
// complianceEligibilityOrganisations (illustrative collection name); g1 is active, g2 is staged
db.complianceEligibilityOrganisations.aggregate([
  { $match: { generation: g2 } },
  {
    $lookup: {
      from: "complianceEligibilityOrganisations",
      let: {
        organisationId: "$organisationId",
        obligationYear: "$obligationYear",
        reviewType: "$reviewType"
      },
      pipeline: [
        {
          $match: {
            $expr: {
              $and: [
                { $eq: ["$generation", g1] },
                { $eq: ["$organisationId", "$$organisationId"] },
                { $eq: ["$obligationYear", "$$obligationYear"] },
                { $eq: ["$reviewType", "$$reviewType"] }
              ]
            }
          }
        },
        { $project: { _id: 0, sourceFingerprint: 1, materializedFingerprint: 1 } },
        { $limit: 1 }
      ],
      as: "previous"
    }
  },
  {
    $set: {
      previousFingerprint: { $arrayElemAt: ["$previous.sourceFingerprint", 0] },
      previousMaterializedFingerprint: { $arrayElemAt: ["$previous.materializedFingerprint", 0] },
      previousCount: { $size: "$previous" }
    }
  },
  {
    $set: {
      change: {
        $switch: {
          branches: [
            { case: { $eq: ["$previousCount", 0] }, then: "Added" },
            { case: { $ne: ["$sourceFingerprint", "$previousFingerprint"] }, then: "SourceChanged" },
            { case: { $ne: ["$materializedFingerprint", "$previousMaterializedFingerprint"] }, then: "ReferenceResolved" }
          ],
          default: "Unchanged"
        }
      }
    }
  },
  { $match: { change: { $ne: "Unchanged" } } },
  { $project: { _id: 0, organisationId: 1, obligationYear: 1, reviewType: 1, change: 1 } }
]);
```

Run the same indexed lookup in reverse—start from `g1`, look up `g2`, retain rows with no match—to identify `Removed` rows. A single `$unionWith` aggregation can combine the two passes, but separate forward/reverse counts are clearer and sufficient for the worker's telemetry.

The existing unique index `{ generation, obligationYear, organisationId, reviewType }` is the required lookup index. The forward pass scans `M` staged rows and performs at most one exact active-generation lookup per row; the reverse pass does the equivalent for `g1`. Its practical cost is therefore linear in the two generations plus indexed lookup overhead, rather than a quadratic cross-product. The exact query plan must be verified with production-like cardinality using `explain("executionStats")`; Mongo version and `$lookup` planning determine how completely the compound index is used with the `let` values.

This comparison is optional in the initial implementation. The only required changed/not-changed decision is the materialised content-fingerprint comparison; after a mismatch, the worker can safely write/promote the complete generation without identifying each changed row. If a staged-generation comparison ever reports no row-level differences after a fingerprint mismatch, treat it as an invariant failure (for example, a fingerprint-version/configuration defect): do not promote that staged generation automatically, record diagnostics, and investigate. It is not the normal no-change path.

| Approach after a sparse change | Mongo writes | Query/correctness cost | Decision |
| --- | --- | --- | --- |
| Full copy on semantic change | Full generation only when the derived set differs. | The existing one-generation query and atomic pointer swap remain simple. | **Recommended initially.** |
| Delta overlay with changed rows and tombstones | Only changed/new/removed rows. | Queries must resolve an overlay chain before filtering, sorting, counting and CSV export; periodic compaction is required. This risks reintroducing mixed-snapshot errors. | Defer unless measured full-copy cost is unacceptable. |
| Update active rows in place | Only changed rows. | Cannot atomically represent changes and removals from a full source read without pending/run markers and delayed deletion. | Not recommended. |
| Source change cursor, event, or ETag | Potentially avoids the full GET too. | Requires Waste Organisations contract support that does not exist today. | Future source-interface improvement. |

The poller's metrics must distinguish `NoChange` from `Promoted`, and record source GET duration/bytes, rows derived, row writes, full-set fingerprint, and promotion duration. Load-test this with production-like population size before deciding whether sparse-change overlays are warranted.

### Generations, promotion, and old data

A generation is the identifier for one *complete, semantically changed* organisation-load result. For example, `g1` represents every individual review row obtained from a successful source response; a later identical response keeps `g1`, whereas the next different result becomes `g2`. It is not a version counter on a single organisation.

#### Why use generations rather than update the active rows in place?

Generations are the recommended design for the organisation projection because Waste Organisations supplies an unpaged full response, not a delta feed or source event stream.

| Approach | Consequence |
| --- | --- |
| Update active rows in place during the load | Readers can observe a mixture of old, changed, and not-yet-written organisations. Safely handling an organisation missing from the new source result requires `lastSeen` markers and delayed deletion. A failed or partial source response can otherwise make valid organisations appear unsubmitted or disappear. |
| Write a staged generation then promote it | Readers observe one complete old result or one complete new result. A failed refresh cannot alter the active population, and rollback is one metadata-pointer update. |

An in-place design can be made safe only by adding pending fields, a refresh/run identifier, delayed removal, and a publish flag. That recreates the essential generation/promotion concept with more complex row-level state and weaker reasoning about mixed reads. The generation design costs temporary duplicate rows and a full bulk write, but that cost is justified because an incomplete eligibility population creates incorrect regulator outcomes.

This decision applies only to the periodically loaded organisation projection. `ComplianceDeclarationReviewState` is different: it is updated in place inside the same Mongo transaction as a live declaration mutation, because its count must take effect immediately and is not sourced from a periodic snapshot.

During a refresh whose fingerprint differs, all new documents are written with `generation: g2` while snapshot metadata still says `activeGeneration: g1`. Query code first reads that metadata and uses the generation value it read throughout the query. Therefore requests during the write continue to read only complete `g1` data. An identical source response writes no `g2` at all.

After validation and the final bulk-write verification, promotion is one atomic update of the small snapshot-metadata document:

```text
before: { activeGeneration: "g1", activeContentFingerprint: "...", lastPromotedAt: ... }
after:  { activeGeneration: "g2", activeContentFingerprint: "...", lastPromotedAt: ... }
```

This does **not** put every eligibility-document write in one large Mongo transaction. The atomic operation is only the pointer swap. A request observes either `g1` or `g2`, never a mixture, because it matches rows by the pointer value it captured.

| Situation | Result |
| --- | --- |
| The complete materialised set is unchanged | No new generation is written. Metadata records a new `lastVerifiedAt`; `g1` remains queryable. |
| A source organisation is unchanged but another row changed | A corresponding `g2` document is still written as part of the complete changed snapshot. It replaces `g1` as queryable data when promoted. |
| Name, registration year, registration status, or resolved reference changes | The `g2` document has the changed values/fingerprint. The `g1` document remains unchanged and is ignored after promotion. |
| An unresolved reference remains unresolved but its retry state changes | No generation is needed: both states are represented by the same `Unresolved` materialised fingerprint and neither is queryable. |
| A previously relevant organisation/type/year is absent from the new source response | No `g2` row is written. It disappears from the active view at promotion because queries no longer read `g1`. |
| Refresh fails or validation fails | `g2` is never promoted. `g1` remains active; incomplete `g2` rows are ignored and later removed. |

After promotion, the previous generation is marked superseded in snapshot history and becomes read-only. Normal endpoint queries never select it, but it must not be deleted immediately: an in-flight request may have read `g1` from metadata just before the pointer moved to `g2` and still needs all of `g1`'s rows.

The cleanup policy should be:

1. Retain the active generation and at least one previous successful generation.
2. Retain a superseded generation for at least the maximum endpoint/request timeout plus a safety grace period; retain it longer (for example 24 hours) to permit investigation and an atomic pointer rollback.
3. Delete generations older than that retention window in bounded background batches, always excluding the currently active generation.
4. Delete an unpromoted failed generation once no refresh owns it; it was never queryable and cannot be a rollback target.

If a serious issue is found in `g2` before `g1` expires, rollback is another atomic metadata update from `activeGeneration: g2` to `activeGeneration: g1`. No organisation documents need to be rewritten. Once `g1` has been cleaned up, rollback requires a new successful source load instead.

The declaration-presence count is different: it is updated transactionally with each declaration mutation and is joined live to whichever organisation generation is active. That means an organisation submission or cancellation takes effect immediately, while organisation registration changes take effect at the next successful snapshot promotion.

### If polling is replaced by event consumption

An event-driven source changes the **writer**, not the required result. The public query must still read one organisation/type/year row that contains its current registration data, materialised reference state/value, and source version; it must still anti-join the live `ComplianceDeclarationReviewState` and locally obtain its organisation-obligation summary. The one-row-per-registration design, materialised reference number, distinct volatile obligation summary, and rule that unresolved references are excluded are therefore ratified by an event model.

Do not create a complete `g(n+1)` generation for every event. That would turn a single organisation update or reference assignment into `O(M)` writes. Instead, use an active, individually mutable read model with the same fields as `ComplianceEligibilityOrganisation`, except that it has no `generation` and adds durable event-processing metadata:

```text
UnsubmittedOrganisationProjection
  organisationId + obligationYear + reviewType   // unique business key
  registrationStatus, name, tradingName, companiesHouseNumber
  referenceNumber, referenceResolutionState
  organisationSourceVersion / sourceOccurredAt
  referenceSourceVersion / referenceResolvedAt
  lastAppliedEventId / updatedAt
```

The event handling rules are:

1. An organisation-registration event upserts only its corresponding rows. It creates `Pending` reference work when no resolved reference is known; a `REGISTERED` row becomes queryable only after the reference condition is also resolved.
2. An Account reference-assignment event first upserts the resolution cache. If the related organisation row already exists, it updates that one row to `Resolved` in the same local transaction as the consumer checkpoint/inbox record. If the Account event arrives first, the cache waits and the later organisation event hydrates the row.
3. **Future only:** a PRN-status event may enqueue only its affected `{ organisationId, obligationYear }` obligation-summary key when its year equals `currentComplianceYear`. The worker calls the canonical organisation-obligation calculation endpoint and recomputes the summary; it does not recreate the calculation from the event. This is not an initial dependency and must not be approximated by polling individual PRNs.
4. A daily obligation-calculation-run-completed event, containing at least the compliance year and durable run ID/watermark, may later be recorded against summaries to prove which calculation run was observed. The initial rolling poll does not depend on it or create a separate daily burst.
5. A cancellation, deletion, or registration-status event updates only the row concerned; it is immediately excluded when no longer `REGISTERED`.
6. Each consumer stores an inbox/checkpoint and rejects duplicate event IDs. Per-source monotonic version or sequence checks are required because Account, organisation, and any future PRN events can be delayed, replayed, or arrive out of order.
7. The existing declaration mutation transaction continues to update `ComplianceDeclarationReviewState` locally. It does not need to wait for any external event stream.

The broker's consumer-group/lock/checkpoint semantics should own multi-host concurrency for this path, rather than the periodic Mongo lease. A local lease remains appropriate for the retained reconciliation job, but should not compete with event consumers to apply the same row without a single-writer/version rule.

Bootstrap is the critical event design problem: take a full source snapshot at a defined event watermark, persist it, then replay events after that watermark before declaring the projection ready. The initial obligation-summary backfill then runs for active current-year eligible organisation keys. Without a supported snapshot-plus-offset contract, retain the periodic full organisation poll as a low-frequency reconciliation/repair job even after events are introduced. It detects missed events, source corrections, and cache/projection drift. Retain a less-frequent current-year obligation-summary reconciliation too.

Event consumption gives per-organisation atomicity, not a globally atomic all-organisations point-in-time view. That is appropriate only if the upstream event contract represents independent organisation changes. The pull model's complete-generation promotion remains the right design while the only trustworthy input is a full, unversioned `GET /organisations`; do not mix in-place event writes into that active generation. The organisation-obligation summary is intentionally different: it is already a per-key mutable projection, so a PRN event updates or queues only one summary and never creates an organisation generation. Both writers may share mapping, reference-resolution, row-validation, and query code behind a projector interface, but only one owns the active organisation read model at a time.

No suitable PRN-state event is emitted by the inspected `epr-prn-common-backend` code today. Its `GET /api/v2/prn/modified-prns` route is a date-window pull response intended for another integration and returns PRN number, status, status date, accreditation year, source-system ID, and obligation year. It omits the recipient `organisationId`, has no durable ordered cursor, and is not used by this design. Waste Obligations must not poll it as a substitute for an event. If lower-latency updates become a future requirement, the suitable contract is an at-least-once PRN-status event with the fields in rule 3. A separate daily calculation-run-completed event would improve observability, but is not needed for the 30-minute rolling poll.

Recommended configuration (values to agree operationally):

```text
ComplianceEligibilityRefresh
  Enabled
  RefreshMode                    // AllYears initially; PerYear only after a load decision
  ObligationYears                // used only by PerYear mode
  PollingIntervalSeconds         // interval between attempted poll starts; 1800 seconds initially proposed
  StartupJitterMaximumSeconds    // avoids every newly deployed host attempting at exactly the same moment
  RequestTimeout
  LeaseDuration
  LeaseRenewalInterval
  MaximumNormalRefreshDuration
  ContentFingerprintVersion      // changes deliberately when canonical persisted-field set changes
  MaximumAllowedStaleness
  MinimumInitialReferenceCoverage
  RetentionGenerations

ComplianceReferenceResolution
  PollingIntervalMinutes
  BatchSize
  MaxConcurrency
  RetryBackoff

ComplianceObligationHydration
  Enabled
  BusinessTimeZone               // Europe/London; current year is previous calendar year throughout January
  RefreshIntervalMinutes         // 30 initially recommended; polls each current-year organisation's calculated obligations
  MaximumSummaryStaleness        // refresh interval plus tolerated queue/retry/recovery margin; alert/retry threshold, not endpoint gate
  WorkerWakeIntervalMinutes      // short interval to acquire the lease and drain newly due/retry work
  MaxDownstreamRequestsPerMinute // 20 initial setting for 500 organisations refreshed every 30 minutes; shared and paced across new, scheduled, and retry work
  BatchSize
  MaxConcurrency                // 2 initially; bounds short downstream bursts independently of the rate cap
  RequestTimeout
  RetryBackoff
  LeaseDuration
  LeaseRenewalInterval
```

The job should emit success/failure, `NoChange`/`Promoted` outcome, duration, source count, rows written, last-verified age, and lease outcome. When an active generation is older than `MaximumAllowedStaleness`, the query endpoint logs an error for platform alerting but continues to return that last known generation. If no active generation exists, it logs an error and returns an empty page because no correct result can be derived.

### Freshness and worst-case staleness

There are three distinct freshness clocks. They must not be reported as though they are the same.

| Clock | Meaning | Can this design directly measure/enforce it? |
| --- | --- | --- |
| Waste Organisations to Waste Obligations | Time from an organisation change being visible in Waste Organisations to the new local generation being promoted. | Yes: record refresh start/end, `lastVerifiedAt`, and `lastPromotedAt`. |
| Account reference to queryability | Time from an Account reference becoming available to an otherwise eligible organisation appearing in an active generation. | Yes locally: record resolver completion and the next materialised-generation promotion; upstream assignment time needs an Account event/watermark. |
| Daily obligation calculation to percentage met | Time from a changed daily `ObligationCalculation` record to the next scheduled current-year organisation-obligation read. | Yes locally: record `lastSuccessfulReadAt`, scheduled due time, coverage, and work latency. |
| Individual PRN-state change to percentage met | Time from a changed individual PRN state to the next scheduled current-year organisation-obligation read. | Yes only as a rolling-interval bound in this first phase. There is deliberately no state event or PRN-change polling. |
| Synapse/Common Data to Waste Obligations | Time from the original source change to the new local generation being promoted. | Only partially. Waste Obligations has no upstream completion event or source watermark. |

For the current Azure Function schedule, the maximum *schedule wait* before the next `UpdateWasteOrganisations` invocation begins is **16 hours 30 minutes**. The final daily invocation begins at 07:31 UTC; a source change that misses that invocation's captured `utcNow` must wait until the next day's 00:01 UTC invocation. During the 00:01–07:31 window the normal maximum wait to a new invocation is 30 minutes, but the overnight gap governs the worst case.

Define the following measured/configured values:

| Symbol | Definition |
| --- | --- |
| `U` | Upstream schedule wait: **16h 30m** under the current cron. |
| `I` | Time from the chosen integration invocation starting until its final organisation update is visible in Waste Organisations. This includes the Common Data delta request, sequential organisation writes, retries, and Waste Organisations processing. |
| `P` | `PollingIntervalMinutes` for Waste Obligations; **30m** is the initial proposal. |
| `R` | Time from the Waste Obligations poll starting to its new generation being atomically promoted, including the source GET, reference-cache lookup/immediate Account batch attempts, transformation and bulk writes. |
| `J` | Bounded scheduler/startup jitter. |
| `A` | Interval of the Account reference-resolution retry worker. |
| `AR` | Time for one bounded Account-resolution batch and its cache write. |
| `T` | `RefreshIntervalMinutes` for each current-year organisation's calculated obligations; **30 minutes** is the initial recommendation. |
| `H` | Queue delay plus one bounded organisation-obligation calculation request, mapping, and local summary upsert after the row becomes due. |
| `E` | Event-delivery and consumer delay, if a suitable PRN event contract exists. |

Provided both services are healthy, the source change is available to the integration function at the next eligible invocation, and `P` is greater than the normal maximum `R`, the end-to-end cadence bound is:

```text
Synapse/Common Data change to active Waste Obligations generation <= U + I + P + R + J
                                                      = 16h 30m + I + P + R + J
```

With the initially proposed 30-minute poll, this is **17 hours + `I` + `R` + `J`**. For example, it is not accurate to claim a 30-minute organisation-data SLA merely because Waste Obligations polls every 30 minutes; the overnight upstream gap alone is 16 hours 30 minutes.

Once a change is already visible in Waste Organisations—whether written by the integration function or a manual update—the normal bound is only:

```text
Waste Organisations change to active Waste Obligations generation <= P + R + J
```

For an organisation whose Account reference is absent during the source poll but becomes available later, the normal additional path is:

```text
Account reference available to queryable active generation <= A + AR + P + R + J
```

This is a local resolution cadence bound, not a guarantee that Account has assigned a reference. While no reference exists or Account repeatedly fails, the organisation remains deliberately excluded and the duration is unbounded; coverage metrics and the agreed fail-closed policy are therefore as important as the source-staleness limit.

For either a changed daily `ObligationCalculation` record or a changed individual PRN state, the initial rolling-poll bound is:

```text
Source input change to locally hydrated percentage <= T + H + J
```

With the recommended 30-minute interval, a change that occurs just after an organisation's read is normally visible within about 30 minutes plus bounded queue/HTTP time. This is not a claim of real-time PRN-state tracking; it is a controlled staleness window. If a future durable PRN-state event is adopted, the targeted path becomes `E + H`; that is outside this initial design. `lastSuccessfulReadAt` says only when Waste Obligations read the calculation endpoint. The current response does not expose a daily-calculation run ID/timestamp, so it cannot prove exactly which daily calculation run was observed.

The first equation is a successful-operation *cadence bound*, not an unconditional service SLA. The code has no global upper limit for `I`: it processes updates sequentially and the volume is unbounded. Repeated Azure Function failures, Common Data delay, missed timer executions, failed Waste Organisations writes, prolonged lease recovery, or a failed local refresh make the true worst case unbounded until the fault is repaired. Measure `I` and `R` at production cardinality, and alert when `MaximumAllowedStaleness` is exceeded while the endpoint returns the last active generation.

For the multi-host worker specifically, a host crash immediately after a lease renewal can add up to `LeaseDuration + P + R + J` before another healthy host promotes a replacement generation. Set the lease duration well below `P`, renew it frequently, and alert on lease-renewal failure so this is a recovery path rather than normal operation.

`MaximumAllowedStaleness` is an alert threshold for the age of the last successful **local** Waste Obligations source verification (`lastVerifiedAt`), including a no-change poll. It cannot prove that Synapse/Common Data is current, because the Waste Organisations interface currently supplies neither an integration-run completion timestamp nor a source watermark. The endpoint does not expose a source timestamp, avoiding a misleading claim that its rows are current as of Synapse. A true enforceable end-to-end freshness SLA requires the upstream flow to publish a successful-run watermark or event; that is outside this first pull-based phase.

## Unsubmitted query interface

`Unsubmitted` is a derived result, not a new declaration status. The agreed working route is a dedicated sub-resource of declaration search:

```text
GET /compliance-declarations/unsubmitted
    ?obligationYear=2026
    &registrationType=DirectProducer
    &search=acme
    &page=1
    &pageSize=20
    &sort=OrganisationName[asc]
```

The route is provisional, but is the route to design and spike against. Its internal equivalent is:

```csharp
Task<UnsubmittedOrganisationsPaged> SearchUnsubmitted(
    UnsubmittedComplianceDeclarationsQuery query,
    CancellationToken cancellationToken);
```

The endpoint accepts only these query parameters in this first design:

| Parameter | Initial rule |
| --- | --- |
| `obligationYear` | Required integer and must equal the calculated `currentComplianceYear`. Return `400` for a historic or future year: this endpoint's metric contract is maintained only for the current compliance year. |
| `registrationType` | Required single value: `DirectProducer` or `ComplianceScheme`. It identifies the review tab; it is not a comma-separated multi-type search in this first endpoint. |
| `search` | Optional generic organisation search, limited to 100 characters. It follows the current declaration search pattern: escaped, case-insensitive contains matching across raw fields available in this projection. An empty or whitespace-only term is treated as no search filter. |
| `page` | Optional 1-based page number; default `1`. |
| `pageSize` | Optional; default `20`, range `1`–`100`, matching declaration search. |
| `sort` | Optional and uses the existing `Field[asc|desc]` syntax. Its field allow-list is deliberately **TBD**. Until fields are agreed, use a deterministic default of raw organisation name then organisation ID. |

It does not accept `status` because status is an internal input to the inference, not a filter users may override.

Required endpoint behaviour:

- `obligationYear` and review `registrationType` are required, and `obligationYear` must equal `currentComplianceYear`;
- page-number pagination follows the existing 1–100 page-size convention;
- the active eligibility snapshot is selected for the requested year; if it is older than `MaximumAllowedStaleness`, log the condition and continue to serve the last complete active generation;
- a candidate is returned only when it has a `REGISTERED` eligibility row with a resolved non-empty reference number and its `unsubmittedExclusionCount` is zero or absent;
- a missing, pending, or stale organisation-obligation summary never excludes an otherwise eligible candidate and never makes a current-obligation calculation request in the handler; return the last successful value when one exists, otherwise the zero/default metric described below;
- return `total`, `page`, `pageSize`, and per-row obligation-summary `asOf` times;
- use a deterministic final tie-breaker of `organisationId`.

The initial response contains the eligibility fields plus the locally hydrated organisation-obligation summary:

```json
{
  "unsubmittedComplianceDeclarations": [
    {
      "organisationId": "...",
      "registrationType": "DirectProducer",
      "organisationName": "...",
      "organisationReferenceNumber": "518293",
      "recyclingObligationsMet": null,
      "obligationCoveragePercentage": 0,
      "obligationDataState": "Pending",
      "obligationsAsOf": null
    }
  ],
  "total": 0,
  "page": 1,
  "pageSize": 20
}
```

`obligationCoveragePercentage: 0` is the safe initial display value, rather than evidence that the organisation has met zero percent of a known obligation. `obligationDataState` makes that distinction available to a client or support investigation: `Pending` has no successful read yet, `Ready` has a current summary, `Stale` exposes its last successful summary outside the target refresh window, and `Failed` has no successful read after a recoverable failure. The page may contain a mixture of these states. `recyclingObligationsMet` remains `null` until an actual summary is available.

### Future operational insight endpoint

The public unsubmitted endpoint is a client-facing list contract and must contain only data required to render, page, and act on that list. It does not expose eligibility-generation freshness or counts of organisations withheld because their reference is unresolved.

A future administration/operational-insight endpoint should provide the corresponding diagnostic state: active-generation promotion and verification times, source freshness, resolved/unresolved reference counts and ages, reference retry/failure/ambiguity counts, organisation-obligation summary state counts, and the oldest pending or stale work. Its authorisation, retention, response shape, and alerting/metric relationship are deliberately separate design work.

### Generic search

The existing compliance-declarations `search` parameter is case-insensitive **contains** matching over four independently persisted fields: `organisation.name`, `organisation.complianceSchemeName`, `organisation.schemeOperatorName`, and `organisation.referenceNumber`. It uses four unanchored regular expressions combined with `OR`. The current Mongo migration explicitly records that this cannot seek a name index; it first uses the obligation-year/status/registration-type filter index, then scans the remaining declarations.

The unsubmitted endpoint deliberately follows that existing, limited approach. It persists raw source fields and searches the fields available to it, with no shared search projection and no change to the compliance-declaration schema:

| Eligibility data available | Generic-search fields |
| --- | --- |
| Active materialised generation | `name`, `tradingName`, and `referenceNumber` |

For an unsubmitted query with a term, apply the work in this order:

```text
1. Match active generation + obligation year + review type + registrationStatus=REGISTERED.
2. Match referenceResolutionState=Resolved with a non-empty referenceNumber.
3. Match escaped, case-insensitive contains regex over name OR tradingName OR referenceNumber.
4. Lookup ComplianceDeclarationReviewState and retain a zero/absent count.
5. Sort, count and page the retained rows.
```

The generic search filter comes before the declaration-state lookup, so a narrow term reduces downstream state lookups. However, contains regex has to inspect every base candidate `C` permitted by steps 1–2; total-count semantics mean it cannot stop once the visible page is full. Its normal operation is therefore `O(C)` candidate inspection, followed by sorting the matching subset and only then the declaration-state lookups. This is the same fundamental limitation as current declaration search, but `C` for unsubmitted organisations may be substantially larger and needs production-cardinality load tests.

The eligibility compound index is `{ generation, obligationYear, reviewType, registrationStatus, referenceResolutionState, name, organisationId }`. It narrows the base candidate set and supports raw-name candidate ordering. It cannot make an unanchored search regex seekable. Do not add a speculative name/trading-name/reference-number index for this contains predicate; it adds write/storage cost without solving the scan. Request validation enforces the 100-character maximum; escape the term as a literal regex, debounce the frontend request, set server-side query timeouts, and measure `C`, scan duration, and result count.

Any future improvement to generic search is deliberately a separate design decision for the wider system. An ordinary Mongo `$in` query is fast only for exact stored values; it cannot retain arbitrary partial contains behaviour. Prefix/token search, n-gram indexing, or a dedicated search capability each change the data/UX/operational trade-off and should be evaluated only if measurements show this current-style query is inadequate.

Reference number is a materialised eligibility field in this design, so it can participate in filtering, search, count, paging, CSV, and any later explicitly-designed sort. Obligation values are also local but are independently mutable and freshness-bounded: the endpoint must not page a list by organisation name and then enrich/sort it by percentage, because that produces a page that is not globally sorted and makes CSV disagree with the UI.

## Mongo query shape

Conceptually the primary query is an anti-join from the active eligibility generation to the compact declaration-presence projection. For the initial name sort, it then joins the local organisation-obligation summary only for the selected page:

```text
active eligibility rows where year + review type + registrationStatus = REGISTERED
  + referenceResolutionState = Resolved + referenceNumber is non-empty
  LEFT JOIN ComplianceDeclarationReviewState where
    organisationId = eligibility.organisationId
    obligationYear = requested year
    registrationType = requested review type
  WHERE unsubmittedExclusionCount is absent or zero
  ORDER BY materialised sort fields, organisationId
  COUNT and PAGE in Mongo
  LEFT JOIN OrganisationObligationSummary on organisationId + obligationYear
    for the selected page; map absence to Pending and map stale/failed state to its last successful value or the zero/default metric
```

Mongo aggregation can express the primary part with `$match`, `$lookup` with a pipeline and `$limit: 1`, a zero/absent-count filter, then `$facet` for the total and page. A second local batch lookup by `{ organisationId, obligationYear }` is usually simpler than a third aggregation lookup for the page rows. The endpoint must query only the active eligibility generation. The compact state avoids scanning or materialising every Submitted/Accepted declaration on every request; the raw declaration collection remains the source for backfill and reconciliation. Pending/stale/failed summary counts should be exposed as metrics, but must not determine whether a valid page is returned.

## Worked example: load, source change, and unsubmitted query

Assume one interval-based job run retrieves these relevant registrations from unfiltered `GET /organisations`:

| Organisation | Source registration | Year | Source status |
| --- | --- | ---: | --- |
| Acme Packaging Ltd (`a1`) | `LARGE_PRODUCER` | 2026 | `REGISTERED` |
| Beta Packaging Ltd (`b2`) | `LARGE_PRODUCER` | 2026 | `REGISTERED` |
| SchemeCo (`c3`) | `COMPLIANCE_SCHEME` | 2026 | `REGISTERED` |

The job maps and bulk-writes three individual documents under generation `g1`. The Direct Producer document for Acme has this shape (BSON names are illustrative):

```json
{
  "generation": "g1",
  "organisationId": "a1",
  "obligationYear": 2026,
  "reviewType": "DirectProducer",
  "name": "Acme Packaging Ltd",
  "tradingName": null,
  "companiesHouseNumber": "12345678",
  "registrationStatus": "REGISTERED",
  "referenceNumber": "518293",
  "referenceResolutionState": "Resolved",
  "sourceFingerprint": "...",
  "materializedFingerprint": "...",
  "refreshedAt": "2026-08-26T08:15:00Z"
}
```

The snapshot metadata switches its `activeGeneration` to `g1` only after all three documents are present. A prior generation remains queryable until this single promotion operation succeeds.

Promotion enqueues the distinct active organisation/year keys for obligation hydration. For example, the worker reads Acme's current obligation calculation directly from the PRN backend, calculates the summary with the Waste Obligations calculator, and upserts:

```json
{
  "organisationId": "a1",
  "obligationYear": 2026,
  "obligationCount": 7,
  "totalAcceptedTonnage": 820,
  "totalObligatedTonnage": 1000,
  "recyclingObligationsMet": false,
  "obligationCoveragePercentage": 82,
  "refreshState": "Ready",
  "lastSuccessfulReadAt": "2026-08-26T08:16:12Z"
}
```

Immediately after `g1` is promoted, Acme can be returned with `obligationCoveragePercentage: 0`, `recyclingObligationsMet: null`, and `obligationDataState: Pending`; the obligation worker is independent of eligibility and progressively replaces that default with the calculated summary. Thereafter the organisation-obligation summary changes independently: a PRN status change for Acme is observed at its next scheduled organisation-obligation refresh and updates this one document; it does not create `g2`.

The declaration-presence projection is separate. Suppose Beta already has a submitted declaration:

```json
{
  "organisationId": "b2",
  "obligationYear": 2026,
  "registrationType": "DirectProducer",
  "unsubmittedExclusionCount": 1,
  "updatedAt": "2026-08-25T10:00:00Z"
}
```

Acme has no corresponding state document, which is equivalent to a count of zero.

### Querying Direct Producer unsubmitted rows

For this request:

```text
GET /compliance-declarations/unsubmitted?obligationYear=2026&registrationType=DirectProducer&page=1&pageSize=20
```

the endpoint does the following in Mongo:

```mermaid
flowchart LR
    A["Active generation g1"] --> B["Match year 2026, DirectProducer, registrationStatus=REGISTERED"]
    B --> C["Match referenceResolutionState=Resolved"]
    C --> D["Lookup declaration-review state by organisation/year/type"]
    D --> E{"unsubmittedExclusionCount > 0?"}
    E -- "Yes" --> F["Exclude Beta"]
    E -- "No / absent" --> G["Include Acme"]
    G --> H["Sort, count and page"]
```

The response contains Acme and has `total: 1`. SchemeCo is not considered because the request selected `DirectProducer`.

If Acme then submits a declaration, the declaration transaction inserts the declaration and increments its `ComplianceDeclarationReviewState` count to `1` together. The next query excludes Acme immediately; it does not wait for an organisation refresh. If the declaration is subsequently cancelled and no other Submitted/Accepted declaration exists, the count returns to `0` and Acme appears again.

### Picking up an organisation status change

On the following day, the integration function maps a source `deleted` producer update to a Waste Organisations registration with status `CANCELLED`. The unfiltered Waste Organisations load still returns Acme and its registration, so Waste Obligations can see the actual new status rather than infer it from a missing filtered result.

The next run writes a new `g2` document for the same organisation/year/review type:

```json
{
  "generation": "g2",
  "organisationId": "a1",
  "obligationYear": 2026,
  "reviewType": "DirectProducer",
  "registrationStatus": "CANCELLED",
  "sourceFingerprint": "changed...",
  "refreshedAt": "2026-08-27T08:15:00Z"
}
```

After `g2` is atomically promoted, Acme no longer passes the `registrationStatus=REGISTERED` eligibility match and is excluded from the unsubmitted view, regardless of its declaration count. Until promotion, requests consistently use `g1`; they never see a partly written `g2`. The job can compare the `g1` and `g2` fingerprints to log the Registered-to-Cancelled transition, but the query does not depend on that comparison.

## Sorting and later enrichment

The present Not submitted table has organisation name, organisation reference number, recycling obligations, and percentage met or Regulation 43. It currently provides no interactive sort headers for that tab, but the new endpoint must still have deterministic ordering.

| Field | First organisation-snapshot phase | Rule for future sortable use |
| --- | --- | --- |
| Organisation name | Store and sort raw `name`, with `organisationId` as tie-breaker. | Matches current declaration approach; deterministic but not an explicit locale-aware order. |
| Organisation reference number | A resolved Account value is materialised into the generation and is required for a row to appear; it can participate in generic search. | Do not enable server sorting until an explicit sort contract and dedicated index are agreed. |
| Recycling obligations / percentage met | Locally hydrated from `OrganisationObligationSummary`; before a first successful read the endpoint returns `null` / `0` with `obligationDataState=Pending`, and a stale/failed refresh retains the last successful value when available. The CSV reads the same local values. | Do not enable sort in the first endpoint. If needed, use a final denormalised read model or measure a full aggregate lookup/sort. |
| Regulation 43 | No declaration exists for an unsubmitted row. | Return `null`/no data; it is not an eligibility attribute. |
| Date submitted | Not applicable. | Do not expose for this result. |

The organisation-obligation summary is deliberately outside the organisation snapshot. An event-driven PRN status/calculation change feed is the best future trigger, while the initial bounded-staleness sweep is the fallback. Calling the organisation-obligation calculation once per row is not an acceptable implementation for either the list or the complete CSV.

## Delivery sequence

1. Agree this source-of-truth behaviour, the eligibility and organisation-obligation refresh windows, zero/default pending-metric behaviour, scheme display-name rule, and sort-field allow-list.
2. Implement the typed Waste Organisations search adapter and contract tests for the combined query.
3. Add the snapshot and reference-resolution collections, indexes, migration documentation, lease, refresh job, immediate Account batch hydration, observability, and failure/staleness handling.
4. Backfill `ComplianceDeclarationReviewState`, add transactional count updates to declaration create/update/delete, and add a reconciliation job/test suite.
5. Add the organisation-obligation summary/work collections, lease worker, non-blocking initial backfill, calculator parity tests, stale sweep, pending/stale metrics, and downstream-failure handling.
6. Implement the internal anti-join/page-summary query with production-like data/explain-plan tests, including pending/no-summary and stale-summary defaults.
7. Add the public review endpoint and update the frontend Not submitted list/count/CSV to use it.
8. Agree/obtain a PRN status/calculation change event or a safe cursor contract before replacing the periodic stale sweep.

## Open decisions

1. What maximum end-to-end staleness is acceptable, accounting for both the upstream Synapse-to-Waste-Organisations schedule and this poller?
2. Resolved: a snapshot older than the limit continues to serve the last complete active generation and logs an error. This preserves the implemented endpoint behaviour while refresh recovery proceeds.
3. What is the definitive scheme display-name rule: Waste Organisations `tradingName`, `name`, or an Account-derived operator name?
4. Should a Cancelled declaration continue to count as not submitted? The current frontend says yes.
5. Which materialised fields should be in the first `sort` allow-list? Organisation name is available; percentage/recycling sorting needs a further read-model or measured full lookup design.
6. Can Account provide and support an explicit maximum batch size and concurrency expectation for both lookup endpoints?
7. Is there a guaranteed single active `isComplianceScheme=true` Account organisation for a Companies House number? If not, who owns resolving an ambiguous match?
8. What reference-coverage policy applies: must initial bootstrap reach 100% before the endpoint is available, and should later unresolved new rows cause `503`, a visible exclusion warning, or both?
9. Resolved: `obligationDataState` is part of the public response so clients can distinguish a safe zero/default from a ready, failed, or stale summary.
10. Can PRN provide an at-least-once status/calculation-change event (or cursor) with recipient `organisationId`, obligation year, event ID, per-key version, and a replay/bootstrap watermark?
11. If events replace polling, which source/version/offset guarantees are available for the bootstrap watermark, organisation registrations, Account reference assignment, and PRN changes?
