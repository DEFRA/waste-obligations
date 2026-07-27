# PRN data integration guide

## Purpose and scope

This guide records the current PRN data sources, Waste Obligations contract boundaries, and the constraints to check before adding or changing a PRN integration.

The first delivery is complete: Waste Obligations exposes the organisation-scoped detail route:

`GET /organisations/{organisationId}/prns/{prnId}`

It calls `epr-prn-common-backend` `GET /api/v1/prn/{prnId}` and maps the upstream detail DTO to the Waste Obligations `Prn` response. This document extends the design guidance to the next proposed delivery:

`GET /organisations/{organisationId}/prns`

The list route must have caller-controlled paging. It is a fact-finding/design guide only; it does not authorise an endpoint, DTO, client, or test implementation.

## Principles

- Waste Obligations owns its public PRN contract. Do not expose an upstream DTO or paging envelope directly.
- The route `organisationId` means the PRN recipient: the producer or compliance scheme that the PRN/PERN was issued to. It does not mean the reprocessor/exporter that issued the note.
- Treat a PRN ID as an opaque string. Common backend detail uses its `externalId` GUID; RREPW/epr-backend uses a Mongo ObjectId string. Do not make the public list/detail contract GUID-only.
- Keep a list projection separate from the full `Prn` detail projection. A source list item must not be deserialised as, or mapped as if it were, a complete detail response.
- Validate the route organisation against the recipient identity returned by the source. Never replace a missing source recipient ID with the route value merely to make a projection appear complete.
- Do not page in Waste Obligations by repeatedly fetching an upstream unpaged collection. Paging, filtering, ordering, and the count/cursor must be performed by the selected source.

## Current Waste Obligations position

The current detail endpoint is implemented in `src/Api/Endpoints/Organisations/Prns/ReadPrn.cs`. It concurrently checks that the organisation exists and reads the upstream PRN. It returns `404` if either lookup fails or if `prn.recipient.organisationId` differs from the route value.

`src/Api/Services/PrnCommonBackend/PrnCommonBackendService.cs` calls:

`GET api/v1/prn/{prnId}` with `X-EPR-ORGANISATION: {organisationId}`

The existing `Dtos.Prn` is a full-detail model. It requires, among other fields, the note type, accreditation year and number, recycling process, regulator/agency, recipient ID, and source-store `createdAt` and `updatedAt`. It is not an appropriate accidental response type for a source that cannot supply those fields.

The closest existing Waste Obligations page-number API is `GET /compliance-declarations`. Its public envelope is:

```json
{
  "complianceDeclarations": [],
  "total": 0,
  "page": 1,
  "pageSize": 20
}
```

For the PRN list, use the same explicit page-number semantics unless a source constraint requires cursor paging to be exposed. The public contract should document a 1-based `page`, a bounded `pageSize`, and a total that means the number of items after all server-side filters. The exact response item fields remain to be agreed before implementation; a compact list-item projection is expected rather than the full detail `Prn` shape.

## Source and identity map

| Source | Current role | Recipient identity | PRN identity suitable for Waste Obligations | Suitability for the proposed list |
| --- | --- | --- | --- | --- |
| `epr-prn-common-backend` | Current detail source and transitional cache for NPWD and RREPW-origin records. | `Eprn.OrganisationId` | `externalId` (GUID rendered as a string) | Candidate now, using its search route with a list-specific projection. |
| RREPW external API | Sync feed consumed by `epr-prn-integration-function`. The current endpoint is implemented by epr-backend's external PRN route. | `issuedToOrganisation.id` is present in each item, but cannot be selected server-side. | `id` string | Not suitable as-is for an organisation-scoped list. |
| `epr-backend` organisation/accreditation API | Current issuer-side operational UI API. | The route organisation is `prn.organisation.id`, which is the issuer, not the recipient. | Mongo ObjectId hex string | Not suitable as-is: wrong scope, extra hierarchy IDs, and no paging. |
| `epr-backend` admin list API | Global operational listing. | Each record contains recipient detail only in some mappings; there is no recipient filter. | Mongo ObjectId hex string | Not suitable as-is: global, privileged route and no recipient filter. |
| `legacy-prns` | Possible future NPWD-only legacy source. | Imported recipient organisation field. | `legacy.externalId` GUID rendered as a string | Not assessed for this list delivery; preserve the identity split if selected later. |

The current RREPW-to-common-backend integration deliberately maps `issuedToOrganisation.id` into common backend `OrganisationId`. In contrast, epr-backend stores the reprocessor/exporter in `prn.organisation.id` and the recipient in `prn.issuedToOrganisation.id`. This role difference is the central constraint for every future source selection.

## Current common-backend endpoint comparison

The two endpoints below are implemented in `epr-prn-common-backend` `PrnController` and use the same `X-EPR-ORGANISATION` header to determine the recipient scope.

| Concern | `GET /api/v1/prn/organisation` | `GET /api/v1/prn/search` | Impact on Waste Obligations |
| --- | --- | --- | --- |
| Recipient scope | `Eprn.OrganisationId == X-EPR-ORGANISATION`. | Same. | Both use the correct organisation role for the Waste Obligations route. |
| Result shape | `List<PrnDto>` populated from the entire `Eprn` entity. | `PaginatedResponseDto<PrnDto>` whose items are an intentionally partial projection. | Select the search endpoint for paging, but introduce a separate upstream search-item model. |
| Paging | None. Reads all matching rows into memory. | `page` and `pageSize`, defaulting to 1 and 10. Returns `currentPage`, `pageSize`, `totalItems`, and computed `pageCount`. | Only the search route meets the paging requirement. |
| Search | None. | Case/collation-dependent SQL `LIKE` against `prnNumber` and `issuedByOrg` only. | Do not describe this as general PRN search or recipient-name search. |
| Filters | None. | Fixed status/material filter tokens. Unknown tokens select all records. | Do not pass frontend filter values through without a Waste Obligations allow-list and an explicit public contract. |
| Sort | Database/default entity order; no explicit order. | Fixed sort tokens; default is issue date descending, then `prnNumber`. | The search route has a deterministic intended order. List response documentation must say what ordering is applied. |
| Empty organisation header | No explicit `Guid.Empty` guard; it returns the matching list, normally empty. | Explicitly returns `401` for `Guid.Empty`. | The behaviours differ. Waste Obligations should validate/authenticate before forwarding and should not depend on either edge case. |
| Empty result | `200 []`. | `200` envelope with `items: []`, `totalItems: 0`, and echoed paging/filter values. | Map the search envelope, not the `/organisation` collection. |

### Search item versus detail item

`/organisation` maps every property on `PrnDto`. `/search` constructs a `PrnDto` but only assigns the fields below:

| Populated by `/search` | Not populated by `/search` even though the response type is `PrnDto` |
| --- | --- |
| `externalId`, `prnNumber`, `materialName`, `organisationName`, `createdOn`, `tonnageValue`, `prnStatusId`, `issuedByOrg`, `issueDate`, `issuerNotes`, `decemberWaste`, `obligationYear` | `organisationId`, `isExport`, `accreditationYear`, `accreditationNumber`, `processToBeUsed`, `reprocessorExporterAgency`, `reprocessingSite`, signatory fields, `lastUpdatedDate`, and the other detail fields. |

ASP.NET Core is not configured to suppress default values. Therefore the absent fields are serialised as default values (`null`, `0`, `false`, `00000000-0000-0000-0000-000000000000`, or `0001-01-01...` as applicable), not as trustworthy source data. In particular, `isExport: false` in a search result cannot establish that the note is a PRN rather than a PERN.

This creates a hard boundary:

- The search item can provide a list summary: source ID, number, status, material, recipient display name, issuer, issued/created date, tonnage, December-waste flag, and obligation year.
- It cannot produce the current full `Dtos.Prn` response correctly.
- Detail navigation must continue to call the existing detail route using the search `externalId`, rather than retaining a partial search item as a detail cache.

### Common-backend search gotchas

1. The route template retains optional path segments (`search/{page?}/{search?}/{filterBy?}/{sortBy?}`), but the controller intentionally ignores them. Values are bound from the query string into `PaginatedRequestDto`. Call `GET /api/v1/prn/search?page=1&pageSize=20`; do not use the obsolete path segments.
2. `page` and `pageSize` have defaults but no validation or upper bound. A zero/negative page or page size can produce invalid or surprising results, and a very large page size can request a large result set. Waste Obligations must validate its own request before making the upstream call.
3. `totalItems` is calculated after search and `filterBy` but before paging. `pageCount` is derived from `totalItems` and `pageSize`; it is zero when `pageSize` is zero.
4. The controller advertises a `400` response but does not validate malformed pagination/filter values itself. Only an empty organisation GUID is explicitly returned as `401`.
5. `typeAhead` is built from every PRN number and issuing organisation for the recipient before search, filter, or paging. It is not necessary for the proposed list route and could grow without bound. It also performs asynchronous database work synchronously with `.Result` inside the repository.
6. Search filtering maps common-backend status/material-specific UI tokens, not public Waste Obligations values. For example, a status value that is not recognised removes the filter rather than returning a client error.
7. The current endpoint has no cancellation-token parameter on the controller/service/repository path. The Waste Obligations adapter should still honour its caller's cancellation token when issuing its HTTP request.
8. Upstream dates are SQL `datetime2` values. In the existing detail adapter, an `Unspecified` `DateTime` is treated as a UTC clock value before making a `DateTimeOffset`. A list adapter must apply the same rule to populated `issueDate` and `createdOn` fields.

## Recommended common-backend list integration boundary

Subject to the open contract question below, the source call should be conceptually:

`GET /api/v1/prn/search?page={page}&pageSize={pageSize}`

with `X-EPR-ORGANISATION: {organisationId}`. Optional upstream search/filter/sort parameters should be omitted in the first list delivery unless the Waste Obligations public API explicitly exposes an equivalent validated feature.

The adapter should:

1. Model the common-backend search response and item shape under `Services/PrnCommonBackend`; do not reuse `PrnDetails`.
2. Accept only a bounded, positive 1-based page and page size at the Waste Obligations boundary. The selected defaults and maximum should match an agreed public API convention, rather than inheriting common-backend's 10-item default.
3. Map the upstream envelope to a Waste Obligations-owned envelope. Preserve the upstream `totalItems` as the public total only when no extra client-side filtering occurs.
4. Map only fields that the search source actually supplies. Do not infer `type`, recipient ID, accreditation properties, or audit `updatedAt` from defaults or the route.
5. Return an empty page for an existing organisation with no matching PRNs. Preserve the existing organisation-scoped endpoint convention of returning `404` when the organisation itself does not exist.
6. Use each item's `externalId` as the list item's opaque `id`, so a client can navigate to the existing detail route. It must not use common-backend's internal integer `id`.

## RREPW candidate assessment

The route consumed by `epr-prn-integration-function` is:

`GET /v1/packaging-recycling-notes?statuses={statuses}&dateFrom={dateFrom}&dateTo={dateTo}&cursor={cursor}`

Its current implementation is in `epr-backend` `src/packaging-recycling-notes/routes/list.js`, and the integration function calls it from `RrepwService.ListPackagingRecyclingNotes`.

| Capability | Current RREPW route | Assessment for `GET /organisations/{organisationId}/prns` |
| --- | --- | --- |
| Paging | Cursor/limit paging. Default limit 200, capped at 500; `hasMore` and optional `nextCursor` returned. | Technically usable only if Waste Obligations exposes cursor paging or the source gains a page-number option. It cannot be converted to exact page numbers reliably without scanning prior pages. |
| Scope | Global status/date query; no `issuedToOrganisation.id` filter. | Blocker. Waste Obligations must not retrieve globally then filter locally, because that gives incomplete pages, leaks an unbounded global scan, and makes totals incorrect. |
| Status coverage | `statuses` is required and accepts only `awaiting_acceptance` and `cancelled`. | Blocker for a general recipient PRN list. Accepted, awaiting cancellation, and any future agreed statuses are not available. |
| Ordering | Mongo query filters by current status/date but orders by `_id` and uses `_id` as cursor. | Cursor is source-specific and should remain opaque. The query is not a recipient list ordering contract and status changes during traversal need source-owner clarification. |
| Detail fields | Exposes source ID, note number where present, status/event dates, issuer and recipient organisation snapshots, accreditation snapshot, note type flag, tonnage, December-waste flag, and issuer notes. | Useful source data, but not a complete Waste Obligations detail response as currently exposed. |
| Missing fields | No obligation year, source-store `createdAt`, or source-store `updatedAt` in the external mapper. `recyclingProcess` is derivable from material/glass-process rules but is not sent directly. | These prevent it from producing the current detail `Prn` contract without an agreed source change or a justified derived-value rule. |
| Authentication | API-gateway client authentication. | A future direct integration needs its own client credentials, scope, operational ownership, and resilience agreement. |

The existing integration function hard-codes `ObligationYear = "2026"` while mapping this feed to common backend. That is cache/sync behaviour, not evidence that the RREPW list supplies an obligation year. Waste Obligations must not repeat that hard-code.

Conclusion: the RREPW endpoint is not a suitable direct source for the proposed recipient-scoped list. It could become one only after it supports recipient filtering, the required status scope, and an agreed paging/total contract. It is still a useful reference for the richer future PRN field model.

## epr-backend candidate assessment

### Current organisation/accreditation routes

`epr-backend` currently provides:

- `GET /v1/organisations/{organisationId}/registrations/{registrationId}/accreditations/{accreditationId}/packaging-recycling-notes`
- `GET /v1/organisations/{organisationId}/registrations/{registrationId}/accreditations/{accreditationId}/packaging-recycling-notes/{prnId}`

Both routes are issuer-side. The list looks up PRNs by `prn.organisation.id`, `registrationId`, and accreditation ID; it returns the complete, unpaged accreditation collection. The detail route verifies the issuer organisation and accreditation, and does not use its `registrationId` parameter in the handler. A Waste Obligations route has only the recipient organisation ID and PRN ID, so it cannot call either route safely or faithfully.

There are also global cursor-paged routes:

- `GET /v1/admin/packaging-recycling-notes`
- the external RREPW route discussed above

Neither has a recipient-organisation filter. The admin route is also a privileged operational API and should not be repurposed as a service-to-service recipient read path.

### Candidate comparison

| Candidate | Recipient-scoped | Paging | Full list status coverage | Suitable now |
| --- | --- | --- | --- | --- |
| epr-backend issuer/accreditation list | No; organisation means issuer. | No. | It returns an accreditation's non-deleted records, but only after issuer hierarchy selection. | No. |
| epr-backend issuer/accreditation detail | No; organisation means issuer. | Not applicable. | One PRN only. | No. |
| epr-backend admin list | No. | Cursor/limit. | Yes, subject to the requested statuses. | No. |
| RREPW external list | No. | Cursor/limit. | No; awaiting acceptance and cancelled only. | No. |
| New epr-backend recipient list | Yes, if implemented against `issuedToOrganisation.id`. | Must be part of the new contract. | Must be agreed. | Best future candidate. |

The epr-backend stored PRN projection is a good future source because it already contains `issuedToOrganisation`, the issuer organisation, accreditation snapshot, note type, tonnage, status history/event dates, and source-store `createdAt`/`updatedAt`. Those fields are not all exposed by any one current route. A future endpoint should return an intentionally designed recipient-scoped projection, not compose the existing issuer-side list with the global admin feed.

The new endpoint should query `issuedToOrganisation.id == {organisationId}` at the source, exclude statuses not agreed for recipient visibility, have a stable sort with a tie-breaker, and provide one coherent paging contract. It should return the stored PRN ID as an opaque string and enough recipient data for Waste Obligations to verify the scope without another lookup.

## Data-contract guidance for future sources

Before replacing or supplementing common backend, establish a source contract that can supply the exact public detail fields and the agreed list fields.

| Waste Obligations data | Common backend detail | Common backend search | RREPW external list | Current epr-backend stored projection |
| --- | --- | --- | --- | --- |
| Opaque PRN ID | Yes: `externalId`. | Yes: `externalId`. | Yes: `id`. | Yes: `id`. |
| Recipient ID | Yes. | No. | Yes in item, not queryable. | Yes. |
| Recipient display/name/trading/registration type | Display only. | Display only. | Name/trading/registration type where present. | Stored snapshot supports it. |
| Note number, status, material, tonnage, December waste | Yes. | Yes. | Yes. | Yes. |
| PRN/PERN type | Yes: `isExport`. | No reliable value; default `false` is not evidence. | Yes: `isExport`. | Stored as `isExport`; some current route projections instead expose waste-processing type. |
| Issued time and authorised-by actor | Yes. | Issued time only. | Yes. | Yes. |
| Accreditation number/year, process, agency, site | Yes. | No. | Accreditation snapshot is present; process requires derivation; no obligation year. | Stored accreditation snapshot supports most values. |
| Obligation year | Yes. | Yes. | No. | Not present in the inspected PRN projection. |
| Created and updated timestamps | Yes. | Created only. | No. | Stored, but not exposed by the current detail response. |
| Accepted/rejected/cancelled timestamps | Not separately available. | Not available. | Available when the events exist. | Stored as status operations/history. |

For dates, public Waste Obligations output remains `DateTimeOffset` at UTC offset zero. A common-backend SQL `datetime2` value read as `DateTimeKind.Unspecified` represents a UTC clock value under the current source convention; attach UTC kind without shifting the clock value. JavaScript/Mongo dates from epr-backend/RREPW are instants and should be modelled as `DateTimeOffset` and normalised to UTC.

## Design and test checklist for a later implementation

1. Agree the public list item and envelope before creating a DTO. Confirm whether the public API uses page/size/total or cursor/limit/hasMore; do not silently translate between pagination schemes.
2. Confirm organisation semantics end to end: authentication claim, route value, source query, returned recipient ID, and the defensive mismatch check must all mean the recipient organisation.
3. Define allowed status, search, filter, and sort values in Waste Obligations. Map source values explicitly and reject unsupported client inputs with `400` rather than relying on source fallbacks.
4. Add a source-specific integration response model and mapping tests that prove only populated source fields are used. Include a search item whose omitted common-backend fields would otherwise serialise as defaults.
5. Test paging boundaries: absent/default values, zero, negatives, maximum, page beyond results, empty results, and total semantics.
6. Test source scope and authorisation: incorrect organisation, missing organisation, upstream `404`, unauthorised upstream result, and recipient mismatch.
7. Test navigation identity: common-backend list `externalId` must retrieve the same note through the current detail route. Do not generate future RREPW links from a cache-local common-backend GUID.
8. When a future source is selected, add contract tests for its source route, date representation, status visibility, ordering, and pagination stability under record/status changes.

## Open questions

1. What exact list response is required by the frontend: which columns/actions, and should it be a compact `PrnListItem` plus `{ total, page, pageSize }` envelope? This must be agreed before adding a new public DTO.
2. What page-size default and maximum should Waste Obligations publish for organisation PRNs? Common backend's 10-item default is an upstream implementation detail; the closest current Waste Obligations paged endpoint uses 20 and a maximum of 100.
3. Does the initial common-backend list need public search, status filter, material filter, or sort controls? If so, which values and semantics are supported rather than merely inherited from the upstream legacy UI tokens?
4. Can the common-backend search route be changed to return a dedicated search DTO or to omit fields it does not populate? Its current partial `PrnDto` is a contract hazard for all consumers.
5. Is common-backend's `typeAhead` response required by any active consumer, and can it be omitted or made independently bounded? It is unrelated to a paged list and currently queries the full recipient dataset.
6. Who owns a recipient-scoped RREPW/epr-backend list endpoint, and what route, authentication, source ownership, and versioning will it have?
7. Should the future source expose page-number pagination with a total, or should Waste Obligations move to cursor pagination? A cursor cannot provide a reliable arbitrary page number/total without source support.
8. Which epr-backend statuses are visible to a recipient list? Current stored values include draft, awaiting authorisation, awaiting acceptance, accepted, awaiting cancellation, cancelled, deleted, and discarded; this differs from both common backend and the RREPW sync feed.
9. Where will `obligationYear` come from for RREPW/epr-backend PRNs? It is absent from the inspected external and stored projections, and the current cache integration hard-codes 2026.
10. Must a future direct source expose `createdAt`, `updatedAt`, all lifecycle event dates, accreditation fields, and recipient enrichment so that it can support the existing full detail `Prn` response? If not, which public detail fields are allowed to become nullable or be removed through an explicitly versioned contract change?
11. During migration, which source is authoritative for an RREPW-origin PRN present both in common backend's cache and epr-backend? How will a client avoid linking the same business note under two different source-local IDs?
12. Will NPWD legacy PRNs be served from common backend, `legacy-prns`, epr-backend, or a mixed transition? The answer must preserve existing GUID-style legacy links while RREPW/epr-backend uses ObjectId-style strings.

## Sources inspected

| Repository | Relevant files |
| --- | --- |
| `waste-obligations` | `src/Api/Endpoints/Organisations/Prns/ReadPrn.cs`, `src/Api/Services/PrnCommonBackend/PrnCommonBackendService.cs`, `src/Api/Services/PrnCommonBackend/PrnDetails.cs`, `src/Api/Services/PrnCommonBackend/Mappers.cs`, `src/Api/Dtos/PackagingRecyclingNote.cs`, `src/Api/Dtos/ComplianceDeclarationsPaged.cs`, `src/Api/Dtos/SearchComplianceDeclarationsRequest.cs` |
| `epr-prn-common-backend` | `src/EPR.PRN.Backend.API/Controllers/PrnController.cs`, `src/EPR.PRN.Backend.API/Repositories/Repository.cs`, `src/EPR.PRN.Backend.API.Common/DTO/PaginatedRequestDto.cs`, `src/EPR.PRN.Backend.API.Common/DTO/PaginatedResponseDto.cs`, `src/EPR.PRN.Backend.API/Dto/PrnBaseDto.cs`, `src/EPR.PRN.Backend.API/Startup.cs` |
| `epr-prn-integration-function` | `src/EprPrnIntegration.Common/RESTServices/RrepwService/RrepwRoutes.cs`, `src/EprPrnIntegration.Common/RESTServices/RrepwService/RrepwService.cs`, `src/EprPrnIntegration.Common/Models/Rrepw/PackagingRecyclingNote.cs`, `src/EprPrnIntegration.Common/Mappers/RrepwMappers.cs` |
| `epr-backend` | `src/packaging-recycling-notes/routes/list.js`, `src/packaging-recycling-notes/routes/get.js`, `src/packaging-recycling-notes/routes/get-by-id.js`, `src/packaging-recycling-notes/routes/admin-list.js`, `src/packaging-recycling-notes/application/external-prn-mapper.js`, `src/packaging-recycling-notes/application/admin-prn-mapper.js`, `src/packaging-recycling-notes/domain/model.js`, `src/packaging-recycling-notes/repository/mongodb.js` |
