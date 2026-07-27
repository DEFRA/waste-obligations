# PRN data integration guide

## Purpose and scope

This guide records the current PRN data sources, Waste Obligations contract boundaries, and the constraints to check before adding or changing a PRN integration.

The first delivery is complete: Waste Obligations exposes the organisation-scoped detail route:

`GET /organisations/{organisationId}/prns/{prnId}`

It calls `epr-prn-common-backend` `GET /api/v1/prn/{prnId}` and maps the upstream detail DTO to the Waste Obligations `Prn` response. Waste Obligations now also exposes the organisation-scoped list route:

`GET /organisations/{organisationId}/prns`

The list route has caller-controlled paging. Its request pattern follows `Defra.WasteObligations.Api.Endpoints.ComplianceDeclarations.SearchComplianceDeclarations`: a request record bound with `[AsParameters]`, a 1-based optional `page`, and an optional bounded `pageSize`. This guide records the implemented common-backend integration and the constraints for later PRN sources.

## Principles

- Waste Obligations owns its public PRN contract. Do not expose an upstream DTO or paging envelope directly.
- The route `organisationId` means the PRN recipient: the producer or compliance scheme that the PRN/PERN was issued to. It does not mean the reprocessor/exporter that issued the note.
- Treat a PRN ID as an opaque string. Common backend detail uses its `externalId` GUID; epr-backend, including its RREPW external projection, uses a Mongo ObjectId string. Do not make the public list/detail contract GUID-only.
- Keep a source-specific search model separate from the public `Prn` DTO. The common-backend search projection now supplies all fields needed for `Prn`, but it remains a distinct downstream contract and may contain default values for fields not used by Waste Obligations.
- Validate the route organisation against the recipient identity returned by the source. Never replace a missing source recipient ID with the route value merely to make a projection appear complete.
- Do not page in Waste Obligations by repeatedly fetching an upstream unpaged collection. Paging, filtering, ordering, and the page-number total must be performed by the selected source.

## Current Waste Obligations position

The current detail endpoint is implemented in `src/Api/Endpoints/Organisations/Prns/ReadPrn.cs`. It concurrently checks that the organisation exists and reads the upstream PRN. It returns `404` if either lookup fails or if `prn.recipient.organisationId` differs from the route value.

`src/Api/Services/PrnCommonBackend/PrnCommonBackendService.cs` calls:

`GET api/v1/prn/{prnId}` with `X-EPR-ORGANISATION: {organisationId}`

The existing `Dtos.Prn` is a full-detail model. It requires, among other fields, the note type, accreditation year and number, recycling process, regulator/agency, recipient ID, and source-store `createdAt` and `updatedAt`. It is the agreed public item type for the proposed list, so the source integration must supply every required value correctly; it must not manufacture them from a partial source item.

The closest existing Waste Obligations page-number API is `GET /compliance-declarations`. Its public envelope is:

```json
{
  "complianceDeclarations": [],
  "total": 0,
  "page": 1,
  "pageSize": 20
}
```

The PRN list follows the `ComplianceDeclarationsPaged` pattern with a `PrnsPaged` public envelope. Its collection is `IEnumerable<Prn>` and its JSON shape is:

```json
{
  "prns": [],
  "total": 0,
  "page": 1,
  "pageSize": 20
}
```

`page` defaults to 1 and must be at least 1; `pageSize` defaults to 20 and must be from 1 to 100. The optional `search` input searches PRN number or issuer organisation name; it does not search the recipient, material, status, or note text. The public total means the number of items after all server-side filters. This is intentionally the existing full `Prn` public model, not a new compact `PrnListItem` DTO. The common-backend search projection now supplies the fields needed to map this model directly. Page-number pagination with a total is required for the government GDS web controls; a cursor-only public contract is not appropriate for this route.

## Source and identity map

| Source | Current role | Recipient identity | PRN identity suitable for Waste Obligations | Suitability for the proposed list |
| --- | --- | --- | --- | --- |
| `epr-prn-common-backend` | Current detail source and transitional cache for NPWD and epr-backend RREPW-origin records. | `Eprn.OrganisationId` | `externalId` (GUID rendered as a string) | Current list source: its search route supplies paging and all fields needed to map each returned `Prn`. |
| `epr-backend` RREPW external API | External/sync projection consumed by `epr-prn-integration-function`; it is an epr-backend interface, not a separate source system. | `issuedToOrganisation.id` is present in each item, but cannot be selected server-side. | `id` string | Not suitable as-is for an organisation-scoped list. |
| `epr-backend` organisation/accreditation API | Current issuer-side operational UI API. | The route organisation is `prn.organisation.id`, which is the issuer, not the recipient. | Mongo ObjectId hex string | Not suitable as-is: wrong scope, extra hierarchy IDs, and no paging. |
| `epr-backend` admin list API | Global operational listing. | Each record contains recipient detail only in some mappings; there is no recipient filter. | Mongo ObjectId hex string | Not suitable as-is: global, privileged route and no recipient filter. |
| `legacy-prns` | Possible future NPWD-only legacy source. | Imported recipient organisation field. | `legacy.externalId` GUID rendered as a string | Not assessed for this list delivery; preserve the identity split if selected later. |

The current epr-backend RREPW-external-to-common-backend integration deliberately maps `issuedToOrganisation.id` into common backend `OrganisationId`. epr-backend stores the reprocessor/exporter in `prn.organisation.id` and the recipient in `prn.issuedToOrganisation.id`. This role difference is the central constraint for every future source selection.

## Current common-backend endpoint comparison

The two endpoints below are implemented in `epr-prn-common-backend` `PrnController` and use the same `X-EPR-ORGANISATION` header to determine the recipient scope.

| Concern | `GET /api/v1/prn/organisation` | `GET /api/v1/prn/search` | Impact on Waste Obligations |
| --- | --- | --- | --- |
| Recipient scope | `Eprn.OrganisationId == X-EPR-ORGANISATION`. | Same. | Both use the correct organisation role for the Waste Obligations route. |
| Result shape | `List<PrnDto>` populated from the entire `Eprn` entity. | `PaginatedResponseDto<PrnDto>` whose items project every field consumed by the Waste Obligations `Prn` mapper, but not every `PrnDto` property. | Select the search endpoint for paging and model its response with a separate upstream search-item model. |
| Paging | None. Reads all matching rows into memory. | `page` and `pageSize`, defaulting to 1 and 10. Returns `currentPage`, `pageSize`, `totalItems`, and computed `pageCount`. | Only the search route meets the paging requirement. |
| Search | None. | Case/collation-dependent SQL `LIKE` against `prnNumber` and `issuedByOrg` only. | Do not describe this as general PRN search or recipient-name search. |
| Filters | None. | Fixed status/material filter tokens. Unknown tokens select all records. | Do not pass frontend filter values through without a Waste Obligations allow-list and an explicit public contract. |
| Sort | Database/default entity order; no explicit order. | Fixed sort tokens; default is issue date descending, then `prnNumber`. | The search route has a deterministic intended order. List response documentation must say what ordering is applied. |
| Empty organisation header | No explicit `Guid.Empty` guard; it returns the matching list, normally empty. | Explicitly returns `401` for `Guid.Empty`. | The behaviours differ. Waste Obligations should validate/authenticate before forwarding and should not depend on either edge case. |
| Empty result | `200 []`. | `200` envelope with `items: []`, `totalItems: 0`, and echoed paging/filter values. | Map the search envelope, not the `/organisation` collection. |

### Search item versus detail item

`/organisation` maps every property on `PrnDto`. `/search` still creates a projection, but it now assigns every source field required for the Waste Obligations public `Prn` contract:

`externalId`, `prnNumber`, `organisationId`, `organisationName`, `reprocessorExporterAgency`, `prnStatusId`, `tonnageValue`, `materialName`, `issuerNotes`, `prnSignatory`, `prnSignatoryPosition`, `issueDate`, `processToBeUsed`, `decemberWaste`, `issuedByOrg`, `accreditationNumber`, `reprocessingSite`, `accreditationYear`, `obligationYear`, `createdOn`, `lastUpdatedDate`, and `isExport`.

It deliberately does not project the other `PrnDto` properties, including the internal integer `id`, producer agency, issuer reference, signature, status-update time, packaging producer, created/updated-by values, or source-system ID. They are not used by the current Waste Obligations public `Prn` mapping and must not be inferred from their serialised defaults.

#### Direct `Dtos.Prn` mapping

The current `/search` item can now hydrate `Dtos.Prn` directly:

| Public `Prn` data | `/search` source fields |
| --- | --- |
| `id`, `number`, `type`, `status` | `externalId`, `prnNumber`, `isExport`, and `prnStatusId`/the derived `prnStatus`. |
| `issuedAt`, `obligationYear`, `decemberWaste`, `material`, `tonnage` | `issueDate`, `obligationYear`, `decemberWaste`, `materialName`, and `tonnageValue`. |
| `issuer`, `recipient`, `authorisedBy` | `issuedByOrg`; `organisationId` and `organisationName`; `prnSignatory` and `prnSignatoryPosition`. |
| Accreditation and processing fields | `accreditationNumber`, `accreditationYear`, `processToBeUsed`, `reprocessingSite`, and `reprocessorExporterAgency`. |
| Notes and audit | `issuerNotes`, `createdOn`, and `lastUpdatedDate`. |

Recipient `name`, `tradingName`, and `registrationType`, together with the three lifecycle-event timestamps, remain nullable in the public contract and may be `null`; their absence does not prevent a valid `Prn` response.

#### Nullability and validation effect

The new projection removes the previous direct-mapping nullability gap. A source-specific search response model must still validate the values required by the public mapper: `required` and `[Required]` do not validate an outbound `Results.Ok` payload automatically, and a persisted legacy record can still contain an invalid blank or default value. Reuse the existing mapping rules for required strings, IDs, years, dates, material, and status. In particular, `isExport` is now projected and can map the PRN/PERN type correctly rather than relying on a default `false` value.

### Current search mapping decision

The search route remains a legacy `PaginatedResponseDto<PrnDto>` contract; no dedicated downstream search DTO was introduced. Its repository projection has been extended to include the fields above so Waste Obligations can adapt it directly:

1. Call `GET /api/v1/prn/search` with the validated page, page size, status filter, sort, and recipient header.
2. Map every returned search item to the existing public `Prn` model, validating the returned recipient identity against the route organisation.
3. Return those mapped values in `PrnsPaged`, preserving the search response's `totalItems`, effective `page`, and effective `pageSize`.

This is one common-backend request per public page. The existing detail route remains the route for `GET /organisations/{organisationId}/prns/{prnId}` and for clients navigating to a specific PRN, but it is not called once per item in the list.

### epr-pom-api-web gateway compatibility

`epr-pom-api-web` exposes the equivalent gateway route:

`GET /api/v1/prn/search`

`PrnController.SearchPrn` delegates without transformation to `PrnService.GetSearchPrns`, which calls `PrnServiceClient.GetSearchPrns`. That client has a base address of `{PrnServiceApi.BaseUrl}/api/` and calls `v1/prn/search`, so its downstream target is exactly `epr-prn-common-backend` `GET /api/v1/prn/search`.

The client deserialises the downstream JSON directly into `PaginatedResponse<PrnModel>` with Json.NET; the controller then returns that same object. There is no separate gateway mapper or response projection. `PrnModel` already contains every field added to the common-backend search projection: `organisationId`, `reprocessorExporterAgency`, `prnSignatory`, `prnSignatoryPosition`, `processToBeUsed`, `accreditationNumber`, `reprocessingSite`, `accreditationYear`, `lastUpdatedDate`, and `isExport`. They therefore survive the gateway unchanged when the updated common-backend deployment is the configured target.

`prnStatus` is the only relevant type conversion: common backend serialises its `EprnStatus` enum as a string, and the gateway's `PrnModel.PrnStatus` is also a string. This is compatible with the existing Waste Obligations status mapping. The gateway's search-client tests currently assert only a minimal response, so add a contract test with every required list field before treating the gateway route as a verified dependency.

### Common-backend search gotchas

1. The route template retains optional path segments (`search/{page?}/{search?}/{filterBy?}/{sortBy?}`), but the controller intentionally ignores them. Values are bound from the query string into `PaginatedRequestDto`. Call `GET /api/v1/prn/search?page=1&pageSize=20`; do not use the obsolete path segments.
2. `page` and `pageSize` have defaults but no validation or upper bound. A zero/negative page or page size can produce invalid or surprising results, and a very large page size can request a large result set. Waste Obligations must validate its own request before making the upstream call.
3. `totalItems` is calculated after search and `filterBy` but before paging. `pageCount` is derived from `totalItems` and `pageSize`; it is zero when `pageSize` is zero.
4. The controller advertises a `400` response but does not validate malformed pagination/filter values itself. Only an empty organisation GUID is explicitly returned as `401`.
5. `typeAhead` is built from every PRN number and issuing organisation for the recipient before search, filter, or paging. It is omitted from the Waste Obligations list contract and ignored by its adapter for now. Common backend will continue to calculate and return it for its own existing consumers until that service changes it. It could grow without bound and performs asynchronous database work synchronously with `.Result` inside the repository.
6. Search filtering maps common-backend status/material-specific UI tokens, not public Waste Obligations values. For example, a status value that is not recognised removes the filter rather than returning a client error.
7. The current endpoint has no cancellation-token parameter on the controller/service/repository path. The Waste Obligations adapter should still honour its caller's cancellation token when issuing its HTTP request.
8. Common backend persists its `datetime2` timestamps as UTC and returns them as UTC values. Map `issueDate`, `createdOn`, and `lastUpdatedDate` directly to a UTC `DateTimeOffset`. The adapter retains an `Unspecified` fallback that attaches UTC without shifting the clock value, protecting against an unexpected legacy or serialisation change.

## Implemented organisation PRN list request and common-backend mapping

The public request uses PRN terminology while retaining the compliance-declaration pagination pattern:

`GET /organisations/{organisationId}/prns?page={page}&pageSize={pageSize}&search={search}&status={status}&sort={sort}`

`SearchOrganisationPrnsRequest` uses the same page/page-size binding, defaults, and validation as `SearchComplianceDeclarationsRequest`:

| Public input | Binding and validation | Effective value | Common-backend request | Decision |
| --- | --- | --- | --- | --- |
| `organisationId` | Required GUID route value. | The recipient organisation ID. | `X-EPR-ORGANISATION: {organisationId}`. | Direct mapping. Continue the existing organisation lookup and recipient-scope protection. |
| `page` | `[FromQuery(Name = "page")] int?`, `[Minimum(1)]`. | `Page ?? 1`. | `page={EffectivePage}`. | Direct mapping. Do not use the upstream default of 1 implicitly; send the effective value. |
| `pageSize` | `[FromQuery(Name = "pageSize")] int?`, `[Range(1, 100)]`. | `PageSize ?? 20`. | `pageSize={EffectivePageSize}`. | Direct mapping. Waste Obligations deliberately caps the request below the unbounded upstream value. |
| `search` | Optional free-text value. | Omitted when no value is supplied. | `search={search}`. | Direct mapping. Searches PRN number or issuer organisation name only; it is not a general PRN or recipient search. The source uses SQL `LIKE`, so `%` and `_` retain wildcard meaning. |
| `status` | Optional single list-status value: `AwaitingAcceptance`, `Accepted`, `Rejected`, or `Cancelled`. Validate against this allow-list; it is not a comma-separated parameter. | No value means every common-backend status. | `filterBy` from the status mapping below; omitted when no status is requested. | Direct status translation. `AwaitingCancellation` is deliberately excluded from the list request contract because common backend cannot return it. |
| `sort` | Optional list-sort value from the allow-list below. | Omitted when no value is supplied. | `sortBy` from the sort mapping below; omitted when no sort is requested. | Let common backend apply its source default when no sort is requested. |

The endpoint binds this record with `[AsParameters]`, as `SearchComplianceDeclarations` does. Invalid page, page-size, status, or sort values are rejected as `400` by Waste Obligations request validation and are not forwarded to common backend. The public response is `PrnsPaged`: complete `Prn` items are mapped, source `totalItems` becomes `total`, and the effective page values become public `page` and `pageSize`. Common backend's `typeAhead` value is not exposed.

The resulting source call is:

`GET /api/v1/prn/search?page={EffectivePage}&pageSize={EffectivePageSize}&search={search?}&filterBy={mappedStatus?}&sortBy={mappedSort}`

with `X-EPR-ORGANISATION: {organisationId}`. Do not use common backend's obsolete optional route segments.

### Inputs not copied from `SearchComplianceDeclarationsRequest`

`SearchComplianceDeclarationsRequest` contains filters for a cross-organisation regulator search. They do not all apply to an organisation-scoped PRN route.

| Compliance-declaration input | Proposed PRN list position | Common-backend mapping and gap |
| --- | --- | --- |
| `obligationYear` | Do not expose in the first PRN list request. | `PaginatedRequestDto` has no obligation-year field and the repository never filters `Eprn.ObligationYear`. Although each result includes an obligation year, Waste Obligations cannot obtain a correct filtered total/page from the source. Add it only when common backend supports a server-side filter. |
| Comma-separated `status` | Do not expose. The PRN list uses one optional `status` value instead. | Common backend has one `filterBy` string, not a status collection. Multiple upstream requests followed by merge/paging would make totals and page boundaries incorrect. |
| `registrationType` | Do not expose. | The route already identifies one recipient organisation. Common-backend search neither receives nor returns a reliable registration type for filtering. |
| `organisationName` | Do not expose. | The route already identifies the recipient organisation. Common backend's `search` parameter does not search recipient name; it searches PRN number and issuing organisation only. |

This is the intended reuse of the compliance-declaration request pattern: consistent pagination mechanics and validation, not copying filters whose meaning or source support differs.

### Deferred PRN-specific inputs

The common-backend list supports the narrow `search` input documented above. Material and `obligationYear` controls remain deferred until their source semantics are agreed:

| Potential public input | Current common-backend capability | Gap or rule |
| --- | --- | --- |
| Comma-separated `status` | Not supported. | Requires a common-backend API enhancement that accepts a set of statuses and applies it before `totalItems`/paging. The initial public contract intentionally supports one status only. |
| `obligationYear` | Not supported. | Requires a common-backend API enhancement that filters `Eprn.ObligationYear` before `totalItems`/paging. |
| `material` | Not independently supported. | Existing `filterBy` values combine awaiting-acceptance status with a material. A general material filter requires a source enhancement. |
| Additional sort options | `sortBy` accepts the legacy sort tokens mapped below. | The supported public sort values are part of the initial request contract. New values require an explicit public-to-source mapping. |

#### Public status map

The list request has one optional public `status` query parameter. Its supported values map to common-backend `filterBy` values as follows:

| Public `status` | Common-backend `filterBy` | Position |
| --- | --- | --- |
| `AwaitingAcceptance` | `awaiting-all` | Supported. |
| `Accepted` | `accepted-all` | Supported. |
| `Rejected` | `rejected-all` | Supported. |
| `Cancelled` | `cancelled-all` | Supported. |

`AwaitingCancellation` is not a possible value of the list `status` parameter. It remains a status in the existing full-detail `Prn` contract; this is a source-capability boundary for the common-backend-backed list only. An undeclared or invalid query value, including `AwaitingCancellation`, is rejected by request validation and is not advertised in OpenAPI.

The comma-separated status pattern used by `SearchComplianceDeclarationsRequest` must not be reused until common backend supports a multi-status predicate. For example, `status=Accepted,Rejected` cannot be reduced to one `filterBy` value, and making two source calls then merging them would corrupt the total, ordering, and page boundary.

#### Public sort map

The list request has an optional `sort` parameter. It uses public names that describe Waste Obligations fields rather than exposing the legacy values. When omitted, Waste Obligations does not send `sortBy`; common backend currently defaults to issue date descending with `prnNumber` as a tie-breaker.

| Proposed public `sort` | Common-backend `sortBy` | Position |
| --- | --- | --- |
| `IssuedAtDescending` | `date-issued-desc` | Supported. This is the current source default. |
| `IssuedAtAscending` | `date-issued-asc` | Supported. |
| `TonnageDescending` | `tonnage-desc` | Supported. |
| `TonnageAscending` | `tonnage-asc` | Supported. |
| `IssuerDescending` | `issued-by-desc` | Supported. |
| `IssuerAscending` | `issued-by-asc` | Supported. |
| `DecemberWasteDescending` | `december-waste-desc` | Supported. There is no ascending counterpart. |
| `MaterialDescending` | `material-desc` | Supported. |
| `MaterialAscending` | `material-asc` | Supported. |

Do not pass unknown public sort values through: common backend silently uses `date-issued-desc`. The public contract must instead return `400` for an unsupported sort, and document that the source uses `prnNumber` as a tie-breaker after the selected primary sort.

#### No safe public material map

The material-bearing `filterBy` values are not a general material filter. They all also apply `awaiting-all` status:

| Common-backend `filterBy` | Actual source predicate | Why it is not a public material filter |
| --- | --- | --- |
| `awaiting-aluminium`, `awaiting-plastic`, `awaiting-steel`, `awaiting-wood` | Awaiting acceptance and the named source material. | Each excludes accepted, rejected, and cancelled PRNs. |
| `awaiting-glassother` | Awaiting acceptance and `Glass Other`. | Waste Obligations calls this material `Glass`; the source also has a separate glass-remelt value. |
| `awaiting-glassremelt` | Awaiting acceptance and `Glass Re-melt`. | Only usable together with the awaiting-acceptance status. |
| `awaiting-paperfiber` | Awaiting acceptance and either `Paper/board` or `Fibre`. | Waste Obligations exposes `Paper` and `Fibre` separately, so this source token cannot distinguish them. |

Do not add a public `material` query parameter until common backend can filter by material independently of status and can distinguish the public material vocabulary.

The adapter should:

1. Model the common-backend search response envelope under `Services/PrnCommonBackend`. Both the detail response and search items use the same neutral `PrnData` source DTO because the search projection now supplies every field required by the public mapper.
2. Map every returned search item directly to `Prn` using the complete projection above. Do not infer fields that are not part of the public contract from source defaults.
3. Validate the returned recipient ID against the route organisation before returning a mapped `Prn`.
4. Map the upstream search envelope to a Waste Obligations-owned envelope. Preserve the upstream `totalItems` as the public total because the first delivery applies no additional client-side filter.
5. Return an empty page for an existing organisation with no matching PRNs. Preserve the existing organisation-scoped endpoint convention of returning `404` when the organisation itself does not exist.
6. Use each item's `externalId` as the opaque list ID. It must not use common-backend's internal integer `id`.

## epr-backend RREPW external-route assessment

The route consumed by `epr-prn-integration-function` is:

`GET /v1/packaging-recycling-notes?statuses={statuses}&dateFrom={dateFrom}&dateTo={dateTo}&cursor={cursor}`

Its current implementation is in `epr-backend` `src/packaging-recycling-notes/routes/list.js`, and the integration function calls it from `RrepwService.ListPackagingRecyclingNotes`. RREPW here is the epr-backend external API projection of the same underlying PRN system; it is not a separate data source from epr-backend's operational and stored PRN views.

| Capability | Current epr-backend RREPW external route | Assessment for `GET /organisations/{organisationId}/prns` |
| --- | --- | --- |
| Paging | Cursor/limit paging. Default limit 200, capped at 500; `hasMore` and optional `nextCursor` returned. | Not suitable. Waste Obligations requires page-number pagination with a total for GDS web controls. Cursor results cannot provide reliable arbitrary page numbers or a total without source support. |
| Scope | Global status/date query; no `issuedToOrganisation.id` filter. | Blocker. Waste Obligations must not retrieve globally then filter locally, because that gives incomplete pages, leaks an unbounded global scan, and makes totals incorrect. |
| Status coverage | `statuses` is required and accepts only `awaiting_acceptance` and `cancelled`. | Blocker for a general recipient PRN list. Accepted, awaiting cancellation, and any future agreed statuses are not available. |
| Ordering | Mongo query filters by current status/date but orders by `_id` and uses `_id` as cursor. | Cursor is source-specific and should remain opaque. The query is not a recipient list ordering contract and status changes during traversal need source-owner clarification. |
| Detail fields | Exposes source ID, note number where present, status/event dates, issuer and recipient organisation snapshots, accreditation snapshot, note type flag, tonnage, December-waste flag, and issuer notes. | Useful source data, but not a complete Waste Obligations detail response as currently exposed. |
| Missing fields | No obligation year, source-store `createdAt`, or source-store `updatedAt` in the external mapper. `recyclingProcess` is derivable from material/glass-process rules but is not sent directly. | These prevent it from producing the current detail `Prn` contract without an agreed source change or a justified derived-value rule. |
| Authentication | API-gateway client authentication. | A future direct integration needs its own client credentials, scope, operational ownership, and resilience agreement. |

The existing integration function hard-codes `ObligationYear = "2026"` while mapping this epr-backend external projection to common backend. That is cache/sync behaviour, not evidence that the external route supplies an obligation year. Waste Obligations must not repeat that hard-code.

Conclusion: the epr-backend RREPW external route is not a suitable direct interface for the proposed recipient-scoped list. It could become one only after it supports recipient filtering, the required status scope, and an agreed paging/total contract. It is still a useful reference for the richer future PRN field model.

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
| epr-backend RREPW external list | No. | Cursor/limit. | No; awaiting acceptance and cancelled only. | No. |
| New epr-backend PRN list | Yes, if implemented against `issuedToOrganisation.id`. | Must return page number, page size, and total. | Must be agreed. | Best future candidate; it will be owned and housed in epr-backend. |

The epr-backend stored PRN projection is a good future source because it already contains `issuedToOrganisation`, the issuer organisation, accreditation snapshot, note type, tonnage, status history/event dates, and source-store `createdAt`/`updatedAt`. Those fields are not all exposed by any one current route. A future PRN list endpoint should return an intentionally designed projection scoped to the recipient organisation, not compose the existing issuer-side list with the global admin feed.

The new endpoint should query `issuedToOrganisation.id == {organisationId}` at the source, exclude statuses not agreed for recipient visibility, have a stable sort with a tie-breaker, and provide one coherent paging contract. It should return the stored PRN ID as an opaque string and enough recipient data for Waste Obligations to verify the scope without another lookup.

## Data-contract guidance for future sources

Before replacing or supplementing common backend, establish a source contract that can supply the exact public detail fields and the agreed list fields.

| Waste Obligations data | Common backend detail | Common backend search | epr-backend RREPW external projection | epr-backend stored projection |
| --- | --- | --- | --- | --- |
| Opaque PRN ID | Yes: `externalId`. | Yes: `externalId`. | Yes: `id`. | Yes: `id`. |
| Recipient ID | Yes. | Yes. | Yes in item, not queryable. | Yes. |
| Recipient display/name/trading/registration type | Display only. | Display only. | Name/trading/registration type where present. | Stored snapshot supports it. |
| Note number, status, material, tonnage, December waste | Yes. | Yes. | Yes. | Yes. |
| PRN/PERN type | Yes: `isExport`. | Yes: `isExport`. | Yes: `isExport`. | Stored as `isExport`; some current route projections instead expose waste-processing type. |
| Issued time and authorised-by actor | Yes. | Yes. | Yes. | Yes. |
| Accreditation number/year, process, agency, site | Yes. | Yes. | Accreditation snapshot is present; process requires derivation; no obligation year. | Stored accreditation snapshot supports most values. |
| Obligation year | Yes. | Yes. | No. | Not present in the inspected PRN projection. |
| Created and updated timestamps | Yes. | Yes. | No. | Stored, but not exposed by the current detail response. |
| Accepted/rejected/cancelled timestamps | Not separately available. | Not available. | Available when the events exist. | Stored as status operations/history. |

For dates, public Waste Obligations output remains `DateTimeOffset` at UTC offset zero. Common backend persists and returns its SQL `datetime2` values as UTC, so its representative source fixture uses `DateTimeKind.Utc`. If an unexpected `DateTimeKind.Unspecified` value is received, attach UTC without shifting the clock value as a defensive fallback. JavaScript/Mongo dates from epr-backend, including its RREPW external projection, are instants and should be modelled as `DateTimeOffset` and normalised to UTC.

## Implemented design and test coverage

1. `SearchOrganisationPrnsRequest` uses `[AsParameters]`, default page 1, default page size 20, `[Minimum(1)]`, and `[Range(1, 100)]`. Its single-status and sort allow-lists exclude compliance-declaration-only filters, comma-separated status values, and `AwaitingCancellation`; an omitted sort is not forwarded to common backend.
2. The endpoint passes the route organisation as the `X-EPR-ORGANISATION` recipient scope, checks the organisation exists, and rejects a returned recipient mismatch with `404`.
3. Tests cover every supplied public status and sort mapping to one outbound `filterBy` or `sortBy` value, omission of `sortBy` when no sort is requested, and invalid inputs that must result in `400` before calling common backend.
4. `PrnsPaged` returns `IEnumerable<Prn> Prns`, `total`, `page`, and `pageSize`. The neutral source `PrnData` DTO is used by both common-backend routes, while adapter and endpoint tests prove the complete search model is deserialised and returned as the existing public `Prn` type. Invalid/default required values continue to be rejected by that mapper.
5. Tests cover default paging values, zero/negative/over-maximum values, maximum `pageSize`, empty results, total semantics, and the outbound default `page=1`/`pageSize=20` values.
6. Tests cover missing organisations, returned recipient mismatch, authorisation, and the common-backend HTTP request including its paging/filter/sort query and organisation header. Upstream `404`/unauthorised propagation remains the standard integration-client failure path and should be exercised when source error-handling semantics change.
7. A navigation test remains useful: common-backend list `externalId` should retrieve the same note through the current detail route. Do not generate future epr-backend links from a cache-local common-backend GUID.
8. When a future source is selected, add contract tests for its source route, date representation, status visibility, ordering, and pagination stability under record/status changes.

## Open questions

1. **Future epr-backend PRN list endpoint.** The endpoint will be owned and housed in `epr-backend`; it must be a new PRN-list route, scoped to the recipient organisation, rather than reuse an existing endpoint.

   - The route should make `organisationId` mean the recipient stored in `issuedToOrganisation.id`, not the issuer stored in `prn.organisation.id`. See the [recipient-versus-issuer distinction](#source-and-identity-map) and [epr-backend candidate assessment](#epr-backend-candidate-assessment).
   - The exact route remains to be agreed. A candidate shape is `GET /v1/organisations/{organisationId}/prns`; it must explicitly define status visibility, stable ordering, filters, and page-number pagination (`page`, `pageSize`, and `total`). Cursor pagination is not appropriate because the consumer uses government GDS web controls. The response must include recipient data needed for scope validation.
   - **Existing epr-backend routes are not available for this integration:**
     - `GET /v1/organisations/{organisationId}/registrations/{registrationId}/accreditations/{accreditationId}/packaging-recycling-notes` is issuer-scoped (`prn.organisation.id`), requires issuer registration/accreditation hierarchy values that Waste Obligations does not have, and is unpaged.
     - `GET /v1/organisations/{organisationId}/registrations/{registrationId}/accreditations/{accreditationId}/packaging-recycling-notes/{prnId}` is also issuer-scoped and returns only one note; its handler does not use the `registrationId` route value.
     - `GET /v1/admin/packaging-recycling-notes` is global and privileged, with no recipient filter. It must not be repurposed as a recipient service-to-service read route.
     - `GET /v1/packaging-recycling-notes` (the current epr-backend RREPW external route) is global status/date/cursor search with no `issuedToOrganisation.id` filter and incomplete status coverage. It is an external projection of the same epr-backend PRN system, not a separate source. See the [RREPW external-route assessment](#epr-backend-rrepw-external-route-assessment).
     - `epr-pom-api-web` `GET /api/v1/prn/search` is a gateway to common backend, not an epr-backend PRN-list source scoped to the recipient organisation; it is not a substitute for the new endpoint.
   - Authentication must be an explicit epr-backend service-to-service contract for Waste Obligations. It must bind or authorise the requested recipient organisation and not accidentally depend on POM front-end user authentication or on a caller-supplied organisation header alone.
   - Source ownership must treat the epr-backend stored PRN document as the authority for the new endpoint. Its RREPW external route is another projection of that same system, not a competing source. The remaining migration question is how records duplicated in common backend's transitional cache are reconciled while both systems are used.
   - Versioning must begin with a published epr-backend `/v1` contract with opaque source IDs and additive compatibility rules. Any route or payload change that affects recipient scoping, paging, status visibility, or field meaning requires an explicitly versioned migration.
2. Which epr-backend statuses are visible in the PRN list for a recipient organisation? Current stored values include draft, awaiting authorisation, awaiting acceptance, accepted, awaiting cancellation, cancelled, deleted, and discarded; this differs from both common backend and epr-backend's RREPW external sync projection.
3. Where will `obligationYear` come from for epr-backend PRNs? It is absent from both the inspected RREPW external projection and stored projection, and the current cache integration hard-codes 2026.
4. Must a future direct source expose `createdAt`, `updatedAt`, all lifecycle event dates, accreditation fields, and recipient enrichment so that it can support the existing full `Prn` list and detail response? If not, which public fields are allowed to become nullable or be removed through an explicitly versioned contract change?
5. During migration, which source is authoritative for an epr-backend PRN present both in common backend's cache and epr-backend? How will a client avoid linking the same business note under two different source-local IDs?
6. Will NPWD legacy PRNs be served from common backend, `legacy-prns`, epr-backend, or a mixed transition? The answer must preserve existing GUID-style legacy links while epr-backend uses ObjectId-style strings.

## Sources inspected

| Repository | Relevant files |
| --- | --- |
| `waste-obligations` | `src/Api/Endpoints/Organisations/Prns/ReadPrn.cs`, `src/Api/Endpoints/Organisations/Prns/SearchPrns.cs`, `src/Api/Services/PrnCommonBackend/PrnCommonBackendService.cs`, `src/Api/Services/PrnCommonBackend/PrnData.cs`, `src/Api/Services/PrnCommonBackend/PrnSearchResponse.cs`, `src/Api/Services/PrnCommonBackend/Mappers.cs`, `src/Api/Dtos/PackagingRecyclingNote.cs`, `src/Api/Dtos/PrnsPaged.cs`, `src/Api/Dtos/SearchOrganisationPrnsRequest.cs`, `src/Api/Dtos/ComplianceDeclarationsPaged.cs`, `src/Api/Dtos/SearchComplianceDeclarationsRequest.cs` |
| `epr-prn-common-backend` | `src/EPR.PRN.Backend.API/Controllers/PrnController.cs`, `src/EPR.PRN.Backend.API/Repositories/Repository.cs`, `src/EPR.PRN.Backend.API.Common/DTO/PaginatedRequestDto.cs`, `src/EPR.PRN.Backend.API.Common/DTO/PaginatedResponseDto.cs`, `src/EPR.PRN.Backend.API/Dto/PrnBaseDto.cs`, `src/EPR.PRN.Backend.API/Startup.cs` |
| `epr-pom-api-web` | `WebApiGateway/WebApiGateway.Api/Controllers/PrnController.cs`, `WebApiGateway/WebApiGateway.Api/Clients/PrnServiceClient.cs`, `WebApiGateway/WebApiGateway.Api/ConfigurationExtensions/HttpClientServiceCollectionExtensions.cs`, `WebApiGateway/WebApiGateway.Core/Models/Prns/PrnModel.cs`, `WebApiGateway/WebApiGateway.UnitTests/Api/Clients/PrnServiceClientTests.cs` |
| `epr-prn-integration-function` | `src/EprPrnIntegration.Common/RESTServices/RrepwService/RrepwRoutes.cs`, `src/EprPrnIntegration.Common/RESTServices/RrepwService/RrepwService.cs`, `src/EprPrnIntegration.Common/Models/Rrepw/PackagingRecyclingNote.cs`, `src/EprPrnIntegration.Common/Mappers/RrepwMappers.cs` |
| `epr-backend` | `src/packaging-recycling-notes/routes/list.js`, `src/packaging-recycling-notes/routes/get.js`, `src/packaging-recycling-notes/routes/get-by-id.js`, `src/packaging-recycling-notes/routes/admin-list.js`, `src/packaging-recycling-notes/application/external-prn-mapper.js`, `src/packaging-recycling-notes/application/admin-prn-mapper.js`, `src/packaging-recycling-notes/domain/model.js`, `src/packaging-recycling-notes/repository/mongodb.js` |
