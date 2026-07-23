# Organisation PRN read endpoint design

## Purpose

Add a Waste Obligations read endpoint that returns the PRN/PERN detail needed by a frontend:

`GET /organisations/{organisationId:guid}/prns/{prnId}`

In the initial common-backend-backed implementation, `organisationId` identifies the producer or compliance scheme that the PRN/PERN was issued to. This route meaning must be made explicit and preserved when a future source is selected; it is not the reprocessor/exporter organisation that issued the note.

`organisationId` remains a GUID. `prnId` should be a string in the Waste Obligations route and response schema so the contract can carry the stable identity used by each PRN pool: common-backend/preserved legacy GUIDs for NPWD PRNs and `epr-backend` Mongo ObjectId strings for RREPW/future PRNs.

The first implementation should map the existing PRN common backend detail endpoint rather than introduce a new data source:

`GET /api/v1/prn/{prnId}` with `X-EPR-ORGANISATION: {organisationId}`

The endpoint should establish a stable PRN schema owned by Waste Obligations. It should not leak the upstream common backend DTO directly, because that DTO contains persistence details and legacy provenance fields that should not become frontend/API concerns.

Search, listing, bulk selection, and CSV export logic are out of scope for the initial field inventory in this document.

## Repository Sources

| Repository | Role in this design | Key local sources inspected |
| --- | --- | --- |
| `waste-obligations` | Target API that will expose the new organisation-scoped PRN endpoint. Existing endpoint style, auth policy, service client pattern, DTO pattern, WireMock tests, and OpenAPI snapshot pattern come from here. | `src/Api/Endpoints/Organisations/Prns/ReadPrn.cs`, `src/Api/Endpoints/Organisations/OrganisationEndpoints.cs`, `src/Api/Services/PrnCommonBackend/PrnCommonBackendService.cs`, `src/Api/Services/PrnCommonBackend/IPrnCommonBackendService.cs`, `src/Api/Endpoints/Organisations/Obligations/ReadObligations.cs`, `tests/Testing/Extensions/WireMock/PrnCommonBackendExtensions.cs` |
| `epr-prn-common-backend` | First upstream source for PRN detail. Its Get PRN endpoint and `PrnDto` are the initial source contract to map. | `src/EPR.PRN.Backend.API/Controllers/PrnController.cs`, `src/EPR.PRN.Backend.API/Services/PrnService.cs`, `src/EPR.PRN.Backend.API/Repositories/Repository.cs`, `src/EPR.PRN.Backend.API/Dto/PrnBaseDto.cs`, `src/EPR.PRN.Backend.API/Dto/PrnDto.cs`, `src/EPR.PRN.Backend.Data/DataModels/EPRN.cs`, `src/EPR.PRN.Backend.API.Common/Enums/EprnStatus.cs` |
| `epr-pom-api-web` | Existing gateway wrapper used by the packaging frontend. It confirms that the current Get PRN path is a passthrough over PRN common backend detail data. | `WebApiGateway/WebApiGateway.Api/Controllers/PrnController.cs`, `WebApiGateway/WebApiGateway.Api/Clients/PrnServiceClient.cs`, `WebApiGateway/WebApiGateway.Core/Models/Prns/PrnModel.cs` |
| `epr-packaging-frontend` | Current rendered frontend contract for specific PRN/PERN pages. It shows which PRN fields are displayed and which values are only derived in the UI. Search/list/CSV sources were deliberately excluded from the initial inventory. | `src/FrontendSchemeRegistration.Application/DTOs/Prns/PrnModel.cs`, `src/FrontendSchemeRegistration.UI/Controllers/Prns/PrnsController.cs`, `src/FrontendSchemeRegistration.UI/Controllers/Prns/PrnsAcceptController.cs`, `src/FrontendSchemeRegistration.UI/Controllers/Prns/PrnsRejectController.cs`, `src/FrontendSchemeRegistration.UI/Mappers/PrnModelMapper.cs`, `src/FrontendSchemeRegistration.UI/Mappers/PrnAvailableAcceptanceYearsResolver.cs`, `src/FrontendSchemeRegistration.UI/ViewModels/Prns/BasePrnViewModel.cs`, `src/FrontendSchemeRegistration.UI/ViewModels/Prns/PrnViewModel.cs`, `src/FrontendSchemeRegistration.UI/Views/Prns/SelectSinglePrn.cshtml`, `src/FrontendSchemeRegistration.UI/Views/PrnsAccept/AcceptSinglePrn.cshtml`, `src/FrontendSchemeRegistration.UI/Views/PrnsAccept/AcceptedPrn.cshtml`, `src/FrontendSchemeRegistration.UI/Views/PrnsReject/RejectSinglePrn.cshtml`, `src/FrontendSchemeRegistration.UI/Views/PrnsReject/RejectedPrn.cshtml`, `src/FrontendSchemeRegistration.UI/Views/Shared/Partials/Prns/_recyclingNoteStatus.cshtml`, `src/FrontendSchemeRegistration.UI/Views/Shared/Partials/Prns/_recyclingNoteDetails.cshtml`, `src/FrontendSchemeRegistration.UI/Resources/PrnDataResourcesLocalizer.cs` |
| `epr-prn-integration-function` | Current RREPR/RREPW sync path into `epr-prn-common-backend`. It fetches new/updated RREPR PRNs, maps them to the common backend v2 create contract, and later syncs accept/reject outcomes back to RREPR. | `src/EprPrnIntegration.Api/Functions/FetchRrepwIssuedPrnsFunction.cs`, `src/EprPrnIntegration.Api/Functions/UpdateRrepwPrnsFunction.cs`, `src/EprPrnIntegration.Common/Mappers/RrepwMappers.cs`, `src/EprPrnIntegration.Common/Models/SavePrnDetailsRequest.cs`, `src/EprPrnIntegration.Common/Models/PrnUpdateStatus.cs`, `src/EprPrnIntegration.Common/RESTServices/RrepwService/RrepwService.cs`, `src/EprPrnIntegration.Common/RESTServices/RrepwService/RrepwRoutes.cs`, `src/EprPrnIntegration.Common/RESTServices/PrnBackendService/PrnService.cs`, `src/EprPrnIntegration.Common/Models/Rrepw/PackagingRecyclingNote.cs`, `src/EprPrnIntegration.Common/Models/Rrepw/Status.cs`, `src/EprPrnIntegration.Common/Models/Rrepw/UserSummary.cs` |
| `legacy-prns` | Candidate future source for NPWD legacy PRN detail only. It imports NPWD-segmented PRN common backend raw data into MongoDB and preserves common-backend identity/provenance in a `Legacy` subdocument. It will never contain RREPW-sourced PRNs. | `src/Api/Services/PrnCommonBackend/PrnCommonBackendService.cs`, `src/Api/Services/PrnCommonBackend/PrnRawDataDto.cs`, `src/Api/Services/PrnCommonBackend/Mappers.cs`, `src/Api/Jobs/MigrateLegacyPrns.cs`, `src/Api/Data/Entities/LegacyPrn.cs`, `src/Api/Data/Entities/Legacy.cs`, `src/Api/Data/LegacyPrnRepository.cs` |
| `epr-backend` | Future PRN integration point. The new Waste Obligations PRN schema must only contain fields that can be supplied from this service, or the design must record the required `epr-backend` additions. | `src/packaging-recycling-notes/domain/model.js`, `src/packaging-recycling-notes/routes/get-by-id.js`, `src/packaging-recycling-notes/routes/list.js`, `src/packaging-recycling-notes/application/external-prn-mapper.js`, `src/packaging-recycling-notes/application/admin-prn-mapper.js`, `src/packaging-recycling-notes/repository/schema.js`, `src/packaging-recycling-notes/domain/get-process-code.js` |
| `epr-frontend` | Current frontend over the `epr-backend` PRN lifecycle. It shows the PRN status values, list groupings, display labels, and cancellation actions expected by the epr-backend-facing UI. | `src/server/common/constants/statuses.js`, `src/server/prns/list-controller.js`, `src/server/prns/helpers/get-status-config.js`, `src/server/prns/cancel-controller.js`, `src/server/prns/cancelled-controller.js` |

## GitHub Source Links

| Repository | Source link |
| --- | --- |
| `epr-prn-common-backend` | [v1 Get PRN endpoint](https://github.com/DEFRA/epr-prn-common-backend/blob/feature/MO-354/src/EPR.PRN.Backend.API/Controllers/PrnController.cs#L39-L72), [raw-data endpoint](https://github.com/DEFRA/epr-prn-common-backend/blob/feature/MO-354/src/EPR.PRN.Backend.API/Controllers/PrnController.cs#L144-L172), [raw-data current segmentation filter](https://github.com/DEFRA/epr-prn-common-backend/blob/feature/MO-354/src/EPR.PRN.Backend.API/Repositories/Repository.cs#L400-L423), [v2 create PRN endpoint](https://github.com/DEFRA/epr-prn-common-backend/blob/feature/MO-354/src/EPR.PRN.Backend.API/Controllers/PrnControllerV2.cs#L27-L44), [v2 create request contract](https://github.com/DEFRA/epr-prn-common-backend/blob/feature/MO-354/src/EPR.PRN.Backend.API.Common/DTO/SavePrnDetailsRequestV2.cs#L5-L30), [PRN upsert identity handling](https://github.com/DEFRA/epr-prn-common-backend/blob/feature/MO-354/src/EPR.PRN.Backend.API/Repositories/Repository.cs#L478-L544) |
| `epr-packaging-frontend` | [PRN model mapper](https://github.com/DEFRA/epr-packaging-frontend/blob/main/src/FrontendSchemeRegistration.UI/Mappers/PrnModelMapper.cs#L13-L51), [selected PRN page](https://github.com/DEFRA/epr-packaging-frontend/blob/main/src/FrontendSchemeRegistration.UI/Views/Prns/SelectSinglePrn.cshtml#L29-L50), [status detail partial](https://github.com/DEFRA/epr-packaging-frontend/blob/main/src/FrontendSchemeRegistration.UI/Views/Shared/Partials/Prns/_recyclingNoteStatus.cshtml#L20-L88), [note detail partial](https://github.com/DEFRA/epr-packaging-frontend/blob/main/src/FrontendSchemeRegistration.UI/Views/Shared/Partials/Prns/_recyclingNoteDetails.cshtml#L15-L103) |
| `epr-prn-integration-function` | [RREPR PRN payload model](https://github.com/DEFRA/epr-prn-integration-function/blob/main/src/EprPrnIntegration.Common/Models/Rrepw/PackagingRecyclingNote.cs#L8-L14), [RREPW status authorisation model](https://github.com/DEFRA/epr-prn-integration-function/blob/main/src/EprPrnIntegration.Common/Models/Rrepw/Status.cs#L7-L25), [RREPW authorised-by user summary](https://github.com/DEFRA/epr-prn-integration-function/blob/main/src/EprPrnIntegration.Common/Models/Rrepw/UserSummary.cs#L7-L14), [RREPR to common-backend PRN mapper](https://github.com/DEFRA/epr-prn-integration-function/blob/main/src/EprPrnIntegration.Common/Mappers/RrepwMappers.cs#L11-L36), [RREPR PRN processing loop](https://github.com/DEFRA/epr-prn-integration-function/blob/main/src/EprPrnIntegration.Api/Functions/FetchRrepwIssuedPrnsFunction.cs#L112-L168), [common-backend v2 POST client](https://github.com/DEFRA/epr-prn-integration-function/blob/main/src/EprPrnIntegration.Common/RESTServices/PrnBackendService/PrnService.cs#L32-L39), [RREPR list statuses](https://github.com/DEFRA/epr-prn-integration-function/blob/main/src/EprPrnIntegration.Common/RESTServices/RrepwService/RrepwService.cs#L37-L48), [RREPW update function polling modified common-backend PRNs](https://github.com/DEFRA/epr-prn-integration-function/blob/main/src/EprPrnIntegration.Api/Functions/UpdateRrepwPrnsFunction.cs#L37-L57), [RREPW accept/reject outbound mapper](https://github.com/DEFRA/epr-prn-integration-function/blob/main/src/EprPrnIntegration.Common/RESTServices/RrepwService/RrepwService.cs#L130-L154), [RREPW accept/reject routes](https://github.com/DEFRA/epr-prn-integration-function/blob/main/src/EprPrnIntegration.Common/RESTServices/RrepwService/RrepwRoutes.cs#L32-L39), [modified PRN status model](https://github.com/DEFRA/epr-prn-integration-function/blob/main/src/EprPrnIntegration.Common/Models/PrnUpdateStatus.cs#L7-L22) |
| `legacy-prns` | [PRN common backend raw-data client](https://github.com/DEFRA/legacy-prns/blob/feature/MO-317/src/Api/Services/PrnCommonBackend/PrnCommonBackendService.cs#L8-L40), [migration job](https://github.com/DEFRA/legacy-prns/blob/feature/MO-317/src/Api/Jobs/MigrateLegacyPrns.cs#L30-L72), [raw-data DTO](https://github.com/DEFRA/legacy-prns/blob/feature/MO-317/src/Api/Services/PrnCommonBackend/PrnRawDataDto.cs#L5-L101), [raw-data to Mongo mapper](https://github.com/DEFRA/legacy-prns/blob/feature/MO-317/src/Api/Services/PrnCommonBackend/Mappers.cs#L8-L57), [Legacy PRN Mongo document](https://github.com/DEFRA/legacy-prns/blob/feature/MO-317/src/Api/Data/Entities/LegacyPrn.cs#L5-L61), [legacy identity subdocument](https://github.com/DEFRA/legacy-prns/blob/feature/MO-317/src/Api/Data/Entities/Legacy.cs#L6-L19), [Mongo ObjectId assignment](https://github.com/DEFRA/legacy-prns/blob/feature/MO-317/src/Api/Data/LegacyPrnRepository.cs#L12-L19) |
| `epr-backend` | [current get endpoint `packagingRecyclingNoteById`](https://github.com/DEFRA/epr-backend/blob/main/src/packaging-recycling-notes/routes/get-by-id.js#L17-L46), [current get response builder](https://github.com/DEFRA/epr-backend/blob/main/src/packaging-recycling-notes/routes/get-by-id.js#L25-L42), [PRN create route storing registration/accreditation context](https://github.com/DEFRA/epr-backend/blob/main/src/packaging-recycling-notes/routes/post.js#L70-L97), [PRN create handler populating context](https://github.com/DEFRA/epr-backend/blob/main/src/packaging-recycling-notes/routes/post.js#L187-L227), [PRN status constants and transitions](https://github.com/DEFRA/epr-backend/blob/main/src/packaging-recycling-notes/domain/model.js#L9-L70), [PRN domain projection](https://github.com/DEFRA/epr-backend/blob/main/src/packaging-recycling-notes/domain/model.js#L220-L250), [external reject endpoint maps rejection to awaiting cancellation](https://github.com/DEFRA/epr-backend/blob/main/src/packaging-recycling-notes/routes/reject.js#L32-L36), [ledger rejected event projection](https://github.com/DEFRA/epr-backend/blob/main/src/packaging-recycling-notes/application/fold-prn-from-tail-events.js#L9-L32), [Mongo repository ID mapping](https://github.com/DEFRA/epr-backend/blob/main/src/packaging-recycling-notes/repository/mongodb.js#L122-L165), [accreditation-scoped list lookup](https://github.com/DEFRA/epr-backend/blob/main/src/packaging-recycling-notes/repository/mongodb.js#L173-L184), [organisation accreditation lookup](https://github.com/DEFRA/epr-backend/blob/main/src/repositories/organisations/mongodb.js#L406-L418), [external PRN status authorisation mapper](https://github.com/DEFRA/epr-backend/blob/main/src/packaging-recycling-notes/application/external-prn-mapper.js#L18-L24), [external recipient organisation mapper](https://github.com/DEFRA/epr-backend/blob/main/src/packaging-recycling-notes/application/external-prn-mapper.js#L41-L52), [external PRN mapper](https://github.com/DEFRA/epr-backend/blob/main/src/packaging-recycling-notes/application/external-prn-mapper.js#L84-L105), [admin PRN mapper](https://github.com/DEFRA/epr-backend/blob/main/src/packaging-recycling-notes/application/admin-prn-mapper.js#L11-L31) |
| `epr-frontend` | [PRN status constants](https://github.com/DEFRA/epr-frontend/blob/main/src/server/common/constants/statuses.js#L1-L8), [PRN list status grouping](https://github.com/DEFRA/epr-frontend/blob/main/src/server/prns/list-controller.js#L64-L78), [PRN status display mapping](https://github.com/DEFRA/epr-frontend/blob/main/src/server/prns/helpers/get-status-config.js#L14-L38), [awaiting-cancellation confirm flow](https://github.com/DEFRA/epr-frontend/blob/main/src/server/prns/cancel-controller.js#L20-L78), [cancelled success guard](https://github.com/DEFRA/epr-frontend/blob/main/src/server/prns/cancelled-controller.js#L14-L31) |

## Current Upstream Contract

PRN common backend exposes:

`GET /api/v1/prn/{prnId}`

It reads the organisation ID from `X-EPR-ORGANISATION` and returns `404` when no PRN exists for that organisation and PRN external ID.

The upstream repository filter is:

- `Eprn.OrganisationId == orgId`
- `Eprn.ExternalId == prnId`

The upstream `PrnDto` includes these detail fields:

| Upstream field | Initial use in this design |
| --- | --- |
| `externalId` | Required. Stable PRN GUID used by clients for specific-note links and forms. |
| `id` | Not required. Internal SQL integer ID. Do not expose as the Waste Obligations PRN identity. |
| `prnNumber` | Required. PRN/PERN evidence number. |
| `organisationId` | Required as `recipient.organisationId`. It identifies the producer or compliance scheme the PRN/PERN was issued to and allows the endpoint to verify that the returned PRN belongs to the route organisation. |
| `organisationName` | Required as `recipient.displayName`. Common backend has already reduced the recipient organisation to the name selected for display; it cannot supply the separate legal name, trading name, or registration type. |
| `producerAgency` | Exclude for now. It is a first-class root field in `legacy-prns`, but it is not rendered today and is not available from the inspected RREPW payload/model. |
| `reprocessorExporterAgency` | Include. Not rendered today, but promoted to a first-class root field by `legacy-prns`. |
| `prnStatus` | Required. Drives status display, status meaning, action buttons, and accept/reject result guards. |
| `prnStatusId` | Not required. Numeric status ID. Avoid exposing as the primary status contract. |
| `tonnageValue` | Required. Rendered in details, PDF tonnage-in-words, and accept result copy. |
| `materialName` | Required. Rendered as material and used for material group copy. |
| `issuerNotes` | Required. Rendered as note with a "not provided" fallback. |
| `issuerReference` | Exclude. Not rendered and stored under `Legacy` by `legacy-prns`. |
| `prnSignatory` | Required. Rendered as authorised by. |
| `prnSignatoryPosition` | Required. Rendered as position, with null currently mapped to an empty string. |
| `signature` | Exclude for now. It is a first-class root field in `legacy-prns`, but it is not rendered today and is not available from the inspected RREPW payload/model. |
| `issueDate` | Required. Rendered as date issued, issue year, and affects material localisation. |
| `processToBeUsed` | Required. Rendered as recycling process, with null currently mapped to an empty string. |
| `decemberWaste` | Required. Rendered as yes/no, drives the December warning, and participates in actionability year logic. |
| `statusUpdatedOn` | Exclude. The common schema exposes event-specific accepted, rejected, and cancelled timestamps instead of a generic current-status timestamp. |
| `issuedByOrg` | Required. Rendered as issued by. |
| `accreditationNumber` | Required. Rendered as accreditation number. |
| `reprocessingSite` | Required for PRNs. Rendered only when the note is a PRN, not a PERN. |
| `accreditationYear` | Include as nullable integer. Not rendered today, but promoted to a first-class root field by `legacy-prns`. |
| `obligationYear` | Required. Rendered in the December warning and used to derive the effective acceptance year. |
| `packagingProducer` | Exclude for now. It is a first-class root field in `legacy-prns`, but it is not rendered today and is not available from the inspected RREPW payload/model as a distinct PRN field. |
| `createdBy` | Not rendered on specific PRN pages. |
| `createdOn` | Include as nullable `audit.createdAt` for administrative use. Available from common backend and preserved by `legacy-prns`; RREPW direct reads return `null` until RREPW/epr-backend can supply equivalent source-store audit semantics. |
| `lastUpdatedBy` | Not rendered on specific PRN pages. |
| `lastUpdatedDate` | Include as nullable `audit.updatedAt` for administrative use. Available from common backend and preserved by `legacy-prns`; RREPW direct reads return `null` until RREPW/epr-backend can supply equivalent source-store audit semantics. |
| `isExport` | Required. Distinguishes PRN from PERN and controls headings, labels, copy, and PRN-only reprocessing site display. |
| `sourceSystemId` | Not rendered. Current common-backend cache segmentation/provenance only. Exclude from the Waste Obligations schema and any frontend contract. |

## PRN Identity Mapping

There is no single global PRN ID shared across RREPR/RREPW, `epr-prn-common-backend`, `legacy-prns`, and `epr-backend`. The target architecture has two distinct PRN pools, each retaining its own stable identity. Common backend also contains transitional RREPW cache copies used by the existing packaging frontend.

| System or layer | Field | Type | Role |
| --- | --- | --- | --- |
| RREPR/RREPW PRN payload | `id` | `string` | Source-local PRN identifier received on first sync. The current integration stores it as common-backend cache provenance. When the same pool is served directly by epr-backend in the new journey, the epr-backend PRN `id` maps to Waste Obligations `Prn.id`. |
| RREPR/RREPW PRN payload | `prnNumber` | `string` | Human/business evidence number. Used by common backend upsert matching, but not the route ID. |
| `epr-prn-integration-function` create request | `sourceSystemId` | `string?` | Current integration-only field set from RREPR/RREPW `PackagingRecyclingNote.Id`. It preserves the originating ID on the cache row and distinguishes RREPW-sourced records from NPWD records, but it is not exposed by the common-backend-backed Waste Obligations contract. |
| `epr-prn-common-backend` PRN cache | `sourceSystemId` | `string?` | Current cache segmentation/provenance field. `null` identifies NPWD-origin records in the current sync queries. For an RREPW cache copy, it carries the source PRN ID, but it is not exposed through Waste Obligations. |
| `epr-prn-common-backend` PRN cache | `externalId` | `Guid` upstream, `string` in the Waste Obligations schema | Generated when a cache row is inserted and preserved on later upserts. It is the stable identity for NPWD PRNs in the common-backend/legacy journey. For an RREPW cache copy, it is the cache-local identity used by the existing packaging frontend, not the canonical identity for the new RREPW-backed journey. |
| `epr-prn-common-backend` PRN cache | `id` | `int` | Internal SQL primary key. Do not expose through Waste Obligations. |
| `legacy-prns` Mongo document | `id` / `_id` | Mongo ObjectId | Migration-local document identity assigned on insert. Do not expose as the Waste Obligations PRN identity for NPWD legacy PRNs. |
| `legacy-prns` legacy subdocument | `legacy.externalId` | `Guid` upstream, `string` in the Waste Obligations schema | Canonical NPWD legacy PRN identity for Waste Obligations when `legacy-prns` serves NPWD PRNs. Preserves existing common-backend/frontend PRN links. |
| `legacy-prns` legacy subdocument | `legacy.prnId` | `int` | Preserved common-backend SQL ID. Do not expose through Waste Obligations. |
| `legacy-prns` legacy subdocument | `legacy.sourceSystemId` | `string?` | Preserved common-backend migration metadata only. Do not expose through Waste Obligations or require from epr-backend. |
| `epr-backend` PRN store | `id` | Mongo ObjectId hex string | Mongo `_id` exposed as `id` by `packagingRecyclingNoteById`; compatible with a string PRN schema ID but not with a GUID-constrained route or DTO property. |

For a new RREPR/RREPW PRN synced into `epr-prn-common-backend`, the integration carries the source PRN `id` into common backend as `sourceSystemId`. Common backend separately generates an `externalId` GUID for its cache row. Both values remain stable within their respective records, but they serve different journeys: the generated GUID identifies the cache copy in the existing `epr-packaging-frontend` journey, while the RREPW/epr-backend ID identifies the PRN in the new RREPW-backed journey.

The `Prn.id` type decision is settled: it must be an opaque string, and the source serving a PRN maps that pool's stable identity into the field:

| Source | Canonical source identity | Waste Obligations field |
| --- | --- | --- |
| `epr-prn-common-backend` in the initial adapter | `externalId` | `Prn.id` for a request served from the common-backend cache. |
| `legacy-prns` for NPWD legacy PRNs | `legacy.externalId` | `Prn.id` |
| `epr-backend` for RREPW/future PRNs | `id` | `Prn.id` |

This does not require a PRN to change identity. NPWD PRNs retain their common-backend GUID when they are served from `legacy-prns`. RREPW/future PRNs in the new journey use their RREPW/epr-backend ID from listing or navigation through detail rendering and subsequent actions.

Long term, Waste Obligations should treat PRN lookup as a lookup by string ID across the available PRN pools:

- the legacy pool for NPWD PRNs, using common-backend `externalId` while `epr-prn-common-backend` is still the source, or `legacy-prns` `legacy.externalId` if `legacy-prns` becomes the source;
- the RREPW/future pool, using epr-backend `id`.

The rollout must preserve that separation. A new RREPW-backed listing or navigation flow must create links using the RREPW/epr-backend ID, and the recipient-scoped detail endpoint must return that same ID. It must not create durable new-journey RREPW links from the `externalId` generated for the old common-backend cache copy. Existing common-backend RREPW GUIDs belong to the existing cache-backed packaging frontend journey and do not require a public GUID-to-ObjectId translation layer in Waste Obligations.

The initial common-backend adapter can technically serve a cached RREPW PRN when called with its cache `externalId`, because common backend does not expose separate detail routes for the two source segments. That transitional capability does not make the cache GUID the canonical identity for a new RREPW-backed listing/detail journey.

If all NPWD PRNs are imported into `epr-backend`, the identity model needs specific care. Imported NPWD records would need to keep their existing common-backend/legacy GUID identity to avoid breaking legacy links, while RREPW/future records use epr-backend ObjectIds. That is not necessarily a literal value collision, because GUID and ObjectId strings have different shapes, but it is a source-model clash if epr-backend assumes every PRN is addressed only by Mongo ObjectId. If this route is chosen, epr-backend must either support mixed PRN ID formats in its read endpoint or expose a separate legacy lookup path.

## RREPR Cache Flow

The current PRN common backend data is a cache of PRN data from RREPR/RREPW, with producer accept/reject actions synced back asynchronously.

The current flow is:

1. `epr-prn-integration-function` runs `FetchRrepwIssuedPrnsFunction`.
2. It calls RREPR/RREPW `v1/packaging-recycling-notes` for `awaitingAcceptance` and `cancelled` PRNs in a date window.
3. `RrepwMappers.Map` maps each `PackagingRecyclingNote` into `SavePrnDetailsRequest`.
4. `PrnService.SavePrn` posts that request to `epr-prn-common-backend` `POST api/v2/prn`.
5. `epr-prn-common-backend` stores the PRN cache row and the packaging frontend reads it through the existing gateway.
6. The frontend accepts/rejects PRNs against the common backend cache.
7. Integration functions read modified common-backend PRNs and sync accepted/rejected outcomes back to RREPR/RREPW.

The common backend v2 create request contains all of the fields proposed for the Waste Obligations detail schema, plus additional cache/provenance fields. The RREPR mapper currently populates the specific-page baseline fields, and the RREPW-ready promoted legacy-root fields, from RREPR and Waste Organisations data:

| Proposed schema input | Current RREPR sync source |
| --- | --- |
| `id` | Common backend generates `externalId`; RREPR/RREPW `PackagingRecyclingNote.Id` is currently sent only for common-backend cache segmentation and does not map to the Waste Obligations schema. |
| `number` | `PackagingRecyclingNote.PrnNumber`. |
| `type` | `PackagingRecyclingNote.IsExport`. |
| `status` | `PackagingRecyclingNote.Status.CurrentStatus`, currently mapped only for awaiting acceptance and cancelled inbound records. |
| `audit.acceptedAt` / `audit.rejectedAt` / `audit.cancelledAt` | Present separately in the RREPW status payload, but not persisted separately on the common-backend PRN detail row. Return `null` when reading through the initial common-backend detail endpoint. |
| `issuedAt` | `PackagingRecyclingNote.Status.AuthorisedAt`. |
| `obligationYear` | Currently hard-coded to `"2026"` in `RrepwMappers.Map`; RREPW is due to provide the real integer value, and `epr-backend` will explicitly store and expose it. |
| `decemberWaste` | `PackagingRecyclingNote.IsDecemberWaste`. |
| `material` | `PackagingRecyclingNote.Accreditation.Material`, with glass process-specific mapping. |
| `recyclingProcess` | Derived from RREPR material. |
| `tonnage` | `PackagingRecyclingNote.TonnageValue`. |
| `issuer.organisationName` | `PackagingRecyclingNote.IssuedByOrganisation.Name`. |
| `recipient.organisationId` | `PackagingRecyclingNote.IssuedToOrganisation.Id`. |
| `recipient.displayName` | `IssuedToOrganisation.Name` or `TradingName`, selected using Waste Organisations registration type, then persisted in common backend as `organisationName`. |
| `recipient.name` / `recipient.tradingName` / `recipient.registrationType` | Not persisted separately by the current common-backend sync. Return `null` when reading through common backend. The original RREPW object carries `name` and `tradingName`; the registration type used by the mapper comes from Waste Organisations. |
| `authorisedBy.name` | `PackagingRecyclingNote.Status.AuthorisedBy.FullName`. |
| `authorisedBy.position` | `PackagingRecyclingNote.Status.AuthorisedBy.JobTitle`. |
| `reprocessingSite` | Formatted from `PackagingRecyclingNote.Accreditation.SiteAddress`. |
| `accreditationNumber` | `PackagingRecyclingNote.Accreditation.AccreditationNumber`. |
| `additionalNotes` | `PackagingRecyclingNote.IssuerNotes`. |
| `reprocessorExporterAgency` | `PackagingRecyclingNote.Accreditation.SubmittedToRegulator`, mapped to the common-backend agency name. |
| `accreditationYear` | `PackagingRecyclingNote.Accreditation.AccreditationYear`. |

### RREPW Field Availability

The frontend baseline needs `isExport`/`type`, `obligationYear`, issuer organisation name, accreditation number, and accreditation site address because those values are rendered by the current specific PRN/PERN pages or are needed to derive values rendered by those pages.

The common schema also includes `reprocessorExporterAgency` and `accreditationYear` because they are first-class root fields in `legacy-prns` and are available from the inspected RREPW payload/model. Other non-rendered `legacy-prns` root fields are deliberately excluded for now when RREPW cannot supply them.

### RREPW Shape And Public Grouping

The inspected RREPW model does inform grouping, but Waste Obligations should not mirror it blindly. RREPW groups PRN detail like this:

- root note fields: `id`, `prnNumber`, `isDecemberWaste`, `isExport`, `tonnageValue`, and `issuerNotes`;
- `status`: `currentStatus`, `authorisedBy`, `authorisedAt`, `acceptedAt`, `rejectedAt`, and `cancelledAt`;
- `issuedByOrganisation`;
- `issuedToOrganisation`;
- `accreditation`: `id`, `accreditationNumber`, `accreditationYear`, `material`, `submittedToRegulator`, `glassRecyclingProcess`, and `siteAddress`.

That shape supports the common schema grouping for `issuer`, `recipient`, and `authorisedBy`. It does not require every RREPW object, or every accreditation-derived field, to become a public nested object.

Do not use `authorisation.name` for the signatory person. It is understandable, but it reads like the name of an authorisation rather than the person who authorised the PRN/PERN. The current common-backend field is `prnSignatory`; RREPW exposes the same concept as `status.authorisedBy.fullName` and `status.authorisedBy.jobTitle`; `epr-backend` internally stores it as the `status.issued.by` actor and maps that actor back to RREPW `status.authorisedBy` in its external mapper. The Waste Obligations schema should therefore use `authorisedBy.name` and `authorisedBy.position`, while keeping the issuing organisation under `issuer.organisationName`.

For the first Waste Obligations schema, keep `accreditationNumber` and `accreditationYear` top-level. That better matches the current common-backend and frontend field names, and avoids creating an `accreditation` object that currently would only be a partial mirror of RREPW.

If future consumers need accreditation as a first-class object, the whole group should be considered together: `accreditation.number`, `accreditation.year`, `accreditation.material`, `accreditation.regulator`, and `accreditation.site`. Do not move only `accreditationNumber` under `reprocessing`, because that mixes two concepts and adds avoidable mapping churn.

### Nested Field Critique

Nesting is useful where it groups multiple fields with the same role or avoids ambiguous top-level names. It is not useful where the wrapper contains a single scalar and only mirrors one upstream shape.

| Current/proposed field | Critique | Recommendation |
| --- | --- | --- |
| `material.name` | Not necessary. The value is a single public material value and the existing Waste Obligations obligations endpoint already exposes material as a scalar `material`. There is no agreed `material.code` or original-source material field in the first PRN contract. | Flatten to `material`. |
| `issuer.organisationName` | Useful. It disambiguates the organisation that issued the PRN/PERN from the authorised person and from the producer/compliance scheme it was issued to. RREPW already models `issuedByOrganisation` as an object, and future `id` or `tradingName` could fit here without renaming the role. | Keep nested. The role noun avoids making `issuedBy` and `authorisedBy` look like two equivalent actors when one is an organisation and the other is a person. |
| `recipient.organisationId` / `recipient.displayName` | Useful. They make the producer/compliance scheme role explicit, allow the returned PRN to be checked against the organisation-scoped route, and map from the same issued-to organisation object in every intended source. `displayName` states that the value is presentation-ready and avoids implying that it is always the legal organisation name. | Keep nested and require both fields. Use the role noun `recipient` rather than the event phrase `issuedTo`. |
| `recipient.name` / `recipient.tradingName` / `recipient.registrationType` | Useful optional source detail. epr-backend snapshots these values on the PRN, while common backend and `legacy-prns` retain only the already-selected display value. | Keep nested and nullable. Normalise `registrationType` to the existing Waste Obligations vocabulary rather than exposing epr-backend strings directly. |
| `authorisedBy.name` / `authorisedBy.position` | Useful. These fields belong together as the authorised-by actor/person details and map cleanly from RREPW `status.authorisedBy`. This is more explicit than `authorisation.name`; `issuer` separately identifies the issuing organisation. | Keep nested. |
| `reprocessing.site` | Not necessary. The wrapper currently contains only one string, RREPW does not have a `reprocessing` object, and current common-backend/frontend language is `reprocessingSite`. | Flatten to `reprocessingSite`. |
| `audit.createdAt` / `audit.updatedAt` / lifecycle event dates | Useful for administrative inspection even though the current PRN frontend does not render them. The common `audit` object groups the available record-audit and PRN-event timestamps while DTO descriptions retain their different semantics. | Keep all audit properties nested and nullable. |

In the RREPW payload currently consumed by `epr-prn-integration-function` through `GET v1/packaging-recycling-notes`, most of those fields are already available:

Important critique: this inspected RREPW route is a list/sync route, not a proven PRN detail route. It is filtered by status, date range, and cursor, and it is used by the Azure Function to cache newly issued/cancelled PRNs into common backend. It should not be assumed to be the endpoint Waste Obligations will call for `GET /organisations/{organisationId}/prns/{prnId}` once PRNs are read directly from RREPW or from an RREPW-backed epr-backend projection. The future integration still needs a single-PRN read contract that can be called from only the Waste Obligations route values, or a documented resolver that supplies any additional route/context IDs.

| Required common-schema field | Current RREPW payload position | Assessment |
| --- | --- | --- |
| `type` / `isExport` | `PackagingRecyclingNote.IsExport`. | Present. |
| `obligationYear` | Not present in the inspected model; `RrepwMappers.Map` currently hard-codes `"2026"`. | Missing today; due to be added as an integer. This remains a blocker until RREPW supplies it. |
| `audit.acceptedAt` | `PackagingRecyclingNote.Status.AcceptedAt`. | Present and nullable because the lifecycle event may not have happened. |
| `audit.rejectedAt` | `PackagingRecyclingNote.Status.RejectedAt`. | Present and nullable because the lifecycle event may not have happened. |
| `audit.cancelledAt` | `PackagingRecyclingNote.Status.CancelledAt`. | Present and nullable because the lifecycle event may not have happened. |
| `issuer.organisationName` | `PackagingRecyclingNote.IssuedByOrganisation.Name`. | Present. |
| `recipient.organisationId` | `PackagingRecyclingNote.IssuedToOrganisation.Id`. | Present. It is the same recipient organisation identity that the current integration maps into common-backend `OrganisationId`. |
| `recipient.displayName` | Derived from `PackagingRecyclingNote.IssuedToOrganisation.Name` or `TradingName` using the recipient registration type from Waste Organisations. | Present through the current integration derivation. A direct source must apply the same display-name rule. |
| `accreditationNumber` | `PackagingRecyclingNote.Accreditation.AccreditationNumber`. | Present. |
| `reprocessingSite` | `PackagingRecyclingNote.Accreditation.SiteAddress`, formatted by the integration mapper. | Present when the RREPW payload contains site address. |
| `reprocessorExporterAgency` | `PackagingRecyclingNote.Accreditation.SubmittedToRegulator`, mapped by `ConvertRegulator`. | Present. |
| `accreditationYear` | `PackagingRecyclingNote.Accreditation.AccreditationYear`. | Present. |

After excluding non-rendered legacy-root fields that RREPW cannot supply, the required non-audit part of the inspected RREPW payload is only missing `obligationYear`, which is due to be added. This means the initial common schema can remain RREPW-ready once the planned integer `obligationYear` is available, without forcing nullable fields into the contract solely because they exist in the current common-backend cache or in `legacy-prns`.

The following `legacy-prns` root fields are not included in the common schema yet:

| Excluded field | Why it is excluded |
| --- | --- |
| `producerAgency` | Not rendered in the current specific PRN/PERN UI and not available from the inspected RREPW payload/model. |
| `signature` | Not rendered in the current specific PRN/PERN UI and not available from the inspected RREPW payload/model. |
| `packagingProducer` | Not rendered in the current specific PRN/PERN UI and not available from the inspected RREPW payload/model as a distinct PRN field. The UI currently renders `organisationName`/recipient organisation as the producer or compliance scheme. |

If RREPW later exposes these values and a consumer needs them, they can be added explicitly. Until then, including them would make Waste Obligations depend on cache/migration fields that a future RREPW/epr-backend source cannot currently satisfy.

### Audit Date Availability And Format

Decision: include source-store and PRN status-event dates together in the common `audit` object as nullable values. They are useful to administrative consumers regardless of whether the current PRN UI renders them. Lack of availability from one source should result in `null`, not removal of the field from the common contract.

`audit.createdAt` and `audit.updatedAt` have source-store semantics: they describe when the PRN record was created or updated in the system serving that PRN and must not be assumed to be directly comparable across PRN pools. `audit.acceptedAt`, `audit.rejectedAt`, and `audit.cancelledAt` have PRN lifecycle-event semantics. DTO and OpenAPI descriptions must make that distinction explicit.

Common backend exposes `createdOn` and `lastUpdatedDate` as `DateTime` on `PrnDto` and `PrnRawDataDto`. New common-backend PRNs set both values from `DateTime.UtcNow`; later common-backend upserts preserve `createdOn` and update `lastUpdatedDate`. `legacy-prns` imports those same values and stores them as root `CreatedAt` and `UpdatedAt`, so `legacy-prns` can supply equivalent NPWD legacy audit values.

The inspected RREPW payload/model does not expose PRN created or updated source-store dates. It exposes lifecycle/status dates under `status`: `authorisedAt`, `acceptedAt`, `rejectedAt`, and `cancelledAt`. Map the accepted, rejected, and cancelled values to their corresponding `audit` properties. `issuedAt` already represents `status.authorisedAt`, so do not duplicate it in `audit`.

`epr-backend` has `createdAt` and `updatedAt` in its PRN domain model. The current `packagingRecyclingNoteById` response only returns `createdAt`, not `updatedAt`. Its route tests show JavaScript `Date` values serialised as UTC ISO strings with milliseconds and `Z`, for example `2026-01-15T10:00:00.000Z`.

Waste Obligations should own the public datetime format rather than passing upstream strings through. Model audit dates as `DateTimeOffset?` in the public DTO and describe them as ISO 8601 extended format with offset, matching existing Waste Obligations public DTOs such as `ComplianceDeclaration.created`, `ComplianceDeclaration.updated`, and `AuditEntry.timestamp`. Current Waste Obligations snapshots show that shape as `2026-04-20T12:28:00+00:00`.

Common schema audit shape:

```json
"audit": {
  "createdAt": "2026-01-15T10:00:00+00:00",
  "updatedAt": "2026-01-15T10:00:00+00:00",
  "acceptedAt": null,
  "rejectedAt": null,
  "cancelledAt": null
}
```

All five properties are nullable `DateTimeOffset` values. For a source that cannot supply a field with the documented semantic, return `null` rather than deriving it from a different timestamp. Add property-level DTO descriptions explaining the semantics and source-specific availability of every field, and emit those descriptions into OpenAPI.

The source-store fields can be populated as:

| Source | `audit.createdAt` | `audit.updatedAt` | Assessment |
| --- | --- | --- | --- |
| `epr-prn-common-backend` | `createdOn` | `lastUpdatedDate` | Available now. Convert `DateTime` to UTC `DateTimeOffset` before returning. |
| `legacy-prns` | `CreatedAt` | `UpdatedAt` | Available if `legacy-prns` becomes the NPWD legacy source; values are imported from common-backend raw data. Convert to UTC `DateTimeOffset`. |
| RREPW direct payload inspected in `epr-prn-integration-function` | Not available | Not available | Return `null` for both fields until RREPW provides source created/updated fields through the selected detail endpoint. |
| `epr-backend` | `createdAt` | Domain has `updatedAt`; current detail response does not return it. | Return `createdAt` when available and `updatedAt` once the future epr-backend PRN detail contract exposes it. Convert from JavaScript/Mongo UTC dates to `DateTimeOffset`. |

The lifecycle-event fields can be populated as:

| Source | `audit.acceptedAt` | `audit.rejectedAt` | `audit.cancelledAt` |
| --- | --- | --- | --- |
| `epr-prn-common-backend` detail | Not available separately; return `null`. | Not available separately; return `null`. | Not available separately; return `null`. |
| `legacy-prns` | Map the matching normalised status-history timestamp when present. | Map the matching normalised status-history timestamp when present. | Map the matching normalised status-history timestamp when present. |
| RREPW direct payload | Map `status.acceptedAt`. | Map `status.rejectedAt`. | Map `status.cancelledAt`. |
| `epr-backend` | Map the accepted status timestamp. | Map the rejected operation timestamp, including while current status is `awaiting_cancellation`. | Map the cancelled status timestamp. |

The new recipient-scoped epr-backend detail endpoint must return these event timestamps from the stored PRN status projection. Do not manufacture a missing event timestamp from `audit.updatedAt`, and do not treat `rejectedAt` as the cancellation timestamp when the PRN has subsequently been cancelled.

If the future RREPW detail/read endpoint selected for Waste Obligations differs from the inspected list/sync payload and cannot supply the included common-schema fields below, that endpoint should be treated as not fit for purpose for the common PRN contract. Request a new RREPW PRN detail endpoint that can supply, at minimum:

- PRN/PERN number;
- PRN/PERN type, either as `isExport` or an explicit note type;
- current status plus accepted, rejected, and cancelled event dates;
- issued date;
- obligation year;
- December waste flag;
- material and glass recycling process where relevant;
- recycling process, or enough material/accreditation data for Waste Obligations to derive it consistently;
- tonnage;
- issued-by organisation name;
- recipient organisation ID, name, trading name, and registration type where available, or enough data to derive the required display name consistently;
- authorised-by person name and position;
- reprocessor/exporter agency or regulator code that Waste Obligations can map;
- accreditation number;
- accreditation year;
- reprocessing site address;
- issuer/additional notes.

## Future `legacy-prns` Integration

`legacy-prns` is a candidate future source for NPWD legacy PRN detail if `epr-prn-common-backend` is decommissioned after migration. It will only ever include NPWD PRNs and will never include RREPW-sourced PRNs. It may also be bypassed if `epr-backend` becomes the single store for both new RREPW PRNs and migrated NPWD legacy PRNs.

The inspected `legacy-prns` code currently:

1. Runs a Hangfire `MigrateLegacyPrns` job.
2. Deletes all existing `LegacyPrn` Mongo documents.
3. Reads paginated PRN common backend raw data from `GET api/v1/prn/raw-data`.
4. Maps each raw PRN into a `LegacyPrn` Mongo document.
5. Assigns a fresh Mongo ObjectId on insert.

The current client sends `sourceSystemId=null` to PRN common backend. In the inspected common-backend repository, that string is interpreted as `SourceSystemId == null`, which selects NPWD-segmented records. That matches the intended `legacy-prns` scope: it should migrate NPWD PRNs only, not every cached PRN and not RREPW-sourced records.

`legacy-prns` stores two groups of PRN data:

- Display/detail data at the document root, such as `PrnNumber`, `Organisation`, `TonnageValue`, `MaterialName`, `Notes`, `PrnSignatory`, `IssueDate`, `IsDecemberWaste`, `AccreditationNumber`, `ReprocessingSite`, `ObligationYear`, `IsExport`, `Status`, `ReprocessorExporterAgency`, and `AccreditationYear`.
- Common-backend provenance under `Legacy`, including SQL `PrnId`, common-backend `ExternalId`, current cache segmentation metadata, numeric `PrnStatusId`, `IssuerReference`, and `ProcessToBeUsed`.

Root fields in `legacy-prns` are a strong signal for the common schema, but not enough on their own. A non-rendered root field should normally only be included now when RREPW can supply it, so the common schema does not become tied to common-backend cache state that future sources cannot satisfy. Nullable source-enrichment fields are an explicit exception where a richer source has stable semantics and older sources can return `null` without fabricating a value.

The inspected code does not currently expose a PRN read API from `legacy-prns`; it provides the migration job and Mongo persistence. If Waste Obligations will read NPWD legacy PRNs directly from `legacy-prns`, a read endpoint or direct service integration still needs to be designed.

### Current `legacy-prns` Position

This is the compatibility analysis against the proposed Waste Obligations schema.

| Waste Obligations field | Current `legacy-prns` position | Gap or action |
| --- | --- | --- |
| `id` | Mongo document has `_id`; `Legacy.ExternalId` preserves common-backend `externalId`. | Map `Legacy.ExternalId` to Waste Obligations `Prn.id` for NPWD legacy PRNs. Do not expose the Mongo `_id` as the public PRN identity. |
| `number` | `PrnNumber`. | No gap, rename to `number`. |
| `type` | `IsExport`. | No gap, map `false` -> `PRN`, `true` -> `PERN`. |
| `status` | `Status.CurrentStatus`, derived from common-backend numeric status ID as `accepted`, `rejected`, `cancelled`, or `awaiting-acceptance`. | Needs normalisation to the Waste Obligations status values. This source preserves `rejected`, unlike current `epr-backend` cancellation lifecycle semantics. |
| `audit.acceptedAt` | `Status.History` contains normalised status and timestamp entries. | Map the matching `accepted` history timestamp when present; otherwise return `null`. |
| `audit.rejectedAt` | `Status.History` contains normalised status and timestamp entries. | Map the matching `rejected` history timestamp when present; otherwise return `null`. |
| `audit.cancelledAt` | `Status.History` contains normalised status and timestamp entries. | Map the matching `cancelled` history timestamp when present; otherwise return `null`. |
| `issuedAt` | `IssueDate`. | No gap. |
| `obligationYear` | `ObligationYear` as nullable integer in the current Mongo document. | Potential data-quality gap for migrated records where invalid common-backend strings were mapped to `null`. Waste Obligations should treat missing/null `obligationYear` as unable to satisfy the required integer contract. |
| `decemberWaste` | `IsDecemberWaste`. | No gap, rename to `decemberWaste`. |
| `material` | `MaterialName`. | No gap for common-backend-migrated data; map the source PRN detail material name into the Waste Obligations PRN material vocabulary. |
| `recyclingProcess` | `Legacy.ProcessToBeUsed`. | No gap, but the field sits under the provenance subdocument rather than root detail data. |
| `tonnage` | `TonnageValue`. | No gap. |
| `issuer.organisationName` | `IssuedByOrg`. | No gap. |
| `recipient.organisationId` | `Organisation.Id`. | No gap. The migration maps common-backend `OrganisationId` into this field. |
| `recipient.displayName` | `Organisation.Name`. | No gap. This value was migrated from common-backend `OrganisationName`, where the display-name selection had already happened. |
| `recipient.name` | Not stored separately from the selected organisation display value. | Return `null`; do not claim that `Organisation.Name` is always the legal name. |
| `recipient.tradingName` | Not stored separately. | Return `null`. |
| `recipient.registrationType` | Not stored on the migrated PRN organisation. | Return `null`. |
| `authorisedBy.name` | `PrnSignatory`. | No gap. |
| `authorisedBy.position` | `PrnSignatoryPosition`. | No gap. |
| `reprocessingSite` | `ReprocessingSite`. | No gap. |
| `accreditationNumber` | `AccreditationNumber`. | No gap. |
| `additionalNotes` | `Notes`, mapped from common-backend `IssuerNotes`. | No gap, rename to `additionalNotes`. |
| `reprocessorExporterAgency` | `ReprocessorExporterAgency`. | No gap. Include because it is root in `legacy-prns` and available from RREPW regulator mapping. |
| `accreditationYear` | `AccreditationYear` as nullable integer. | No gap. Include because it is root in `legacy-prns` and available from RREPW accreditation data. |

The following `legacy-prns` root fields remain intentionally outside the Waste Obligations common schema for now: `ProducerAgency`, `Signature`, and `PackagingProducer`. They are not rendered by the current specific PRN/PERN UI and are not available as source PRN values from the inspected RREPW payload/model. `CreatedAt` and `UpdatedAt` are exposed through the nullable `audit` object described above, using source-store audit semantics. `IssuerReference`, `PrnId`, `ExternalId`, `SourceSystemId`, and numeric `PrnStatusId` remain excluded because they are under the `Legacy` provenance subdocument.

## Future `epr-backend` Integration

`epr-backend` is a future integration point for PRN detail. That means every field in the Waste Obligations PRN schema must either already be available from `epr-backend`, or the design must record the required `epr-backend` change. If `epr-backend` becomes the source, the [recipient-scoped detail endpoint described below](#required-recipient-scoped-epr-backend-detail-endpoint) is required; the current issuer- and accreditation-scoped endpoint is not a suitable integration target for Waste Obligations.

The current `epr-backend` get endpoint is:

- endpoint export name: `packagingRecyclingNoteById`
- path constant name: `packagingRecyclingNoteByIdPath`
- path: `GET /v1/organisations/{organisationId}/registrations/{registrationId}/accreditations/{accreditationId}/packaging-recycling-notes/{prnId}`

The current endpoint response builder returns `id`, `accreditationYear`, `createdAt`, `isDecemberWaste`, `issuedAt`, `issuedBy`, `issuedToOrganisation`, `material`, `notes`, `prnNumber`, `processToBeUsed`, `status`, `tonnage`, and `wasteProcessingType`.

### `epr-backend` Route Hierarchy IDs

The current `epr-backend` read endpoint is accreditation-scoped, so its route needs `registrationId` and `accreditationId` as well as `organisationId` and `prnId`.

Those IDs come from `epr-backend` organisation and PRN data:

| Route parameter | Source in `epr-backend` | How it is populated today |
| --- | --- | --- |
| `organisationId` | `PackagingRecyclingNote.organisation.id` and organisation document `id`. This is the reprocessor/exporter organisation that issued the PRN/PERN. | Supplied by the caller and checked against `prn.organisation.id` in the current single-PRN handler. |
| `registrationId` | `PackagingRecyclingNote.registrationId`; originally an `Organisation.registrations[].id`. | Supplied by the caller on PRN create/list routes. The PRN create route copies the route `registrationId` into the PRN document. The single-PRN get route currently includes this parameter in the path but does not read it from `params`. |
| `accreditationId` | `PackagingRecyclingNote.accreditation.id`; originally an `Organisation.accreditations[].id`, usually linked from `Organisation.registrations[].accreditationId`. | Supplied by the caller on PRN create/list/read routes. The create route snapshots the accreditation into the PRN document; the single-PRN get route fetches the current accreditation and checks the PRN snapshot accreditation ID matches the route value. |
| `prnId` | `PackagingRecyclingNote.id`. | Mongo ObjectId hex string, not a GUID and not the common-backend `externalId`. |

There is an additional organisation-scope mismatch beyond the missing route hierarchy IDs:

- In the current common-backend path, `epr-prn-integration-function` maps `PackagingRecyclingNote.IssuedToOrganisation.Id` into common-backend `OrganisationId`. Common backend then filters `Eprn.OrganisationId` using the `X-EPR-ORGANISATION` header. The Waste Obligations route organisation therefore identifies the producer or compliance scheme that the note was issued to.
- In `epr-backend`, `PackagingRecyclingNote.organisation` is populated from the reprocessor/exporter organisation in the create route. The external mapper exposes it as `issuedByOrganisation`, and the current single-PRN handler checks its route `organisationId` against `prn.organisation.id`. The epr-backend route organisation therefore identifies the organisation that issued the note.

For example, if reprocessor/exporter organisation A issues a PRN to producer organisation B, common backend stores B as the PRN `OrganisationId`, while epr-backend stores A as `prn.organisation.id` and B as `prn.issuedToOrganisation.id`. The Waste Obligations route is currently called with B. Passing B to the existing epr-backend single-PRN route would fail its issuer-organisation check because that route expects A.

For `epr-backend` PRNs created through its own PRN create route, the canonical values for a later detail read should be:

- `registrationId`: the stored `PackagingRecyclingNote.registrationId`;
- `accreditationId`: the stored `PackagingRecyclingNote.accreditation.id`.

If only an organisation document is available, `registrationId` can be derived by finding the registration whose `accreditationId` matches the selected accreditation ID. That should be treated as a resolver fallback, not the source of truth for an existing PRN, because a PRN stores the registration and accreditation context it was created under.

The proposed Waste Obligations endpoint does not have these route parameters:

`GET /organisations/{organisationId:guid}/prns/{prnId}`

So Waste Obligations cannot call the current `epr-backend` `packagingRecyclingNoteById` endpoint using only the proposed route values. Resolving `registrationId` and `accreditationId` alone is not sufficient, because the two routes also assign different roles to `organisationId`.

For the common-backend-first implementation, `registrationId` and `accreditationId` are not populated because they are not needed by `epr-prn-common-backend` `GET /api/v1/prn/{prnId}`.

### Required Recipient-Scoped `epr-backend` Detail Endpoint

If Waste Obligations reads RREPW/future PRNs from `epr-backend`, a new recipient-scoped detail endpoint is required. Waste Obligations should not call the current `packagingRecyclingNoteById` endpoint by attempting to discover and supply the issuer organisation, registration, and accreditation hierarchy.

The recommended semantic shape is:

`GET /v1/organisations/{organisationId}/received-packaging-recycling-notes/{prnId}`

The exact path name remains an `epr-backend` API decision, but `organisationId` must unambiguously mean the producer or compliance scheme in `prn.issuedToOrganisation.id`. Using `received-packaging-recycling-notes` in the path would distinguish this recipient-facing resource from the existing issuer-facing route. An alternative path is acceptable only if it documents and enforces the same recipient scope.

A new endpoint is required for these reasons:

1. **The organisation boundary is different.** Waste Obligations and common backend scope the read to the organisation the PRN/PERN was issued to. The current epr-backend detail route scopes the read to the reprocessor/exporter that issued it. Reusing that route would change the meaning of the public Waste Obligations `organisationId`.
2. **Issuer hierarchy IDs are not part of the consumer identity.** `registrationId` and `accreditationId` describe the issuer context. Waste Obligations receives only the recipient organisation ID and PRN ID, and a producer-facing consumer should not have to discover or supply the issuer's internal route hierarchy.
3. **The source should enforce recipient access.** The service that owns the PRN has `issuedToOrganisation.id` and should enforce that relationship before returning the record. Fetching through an issuer-scoped route and checking the recipient only in Waste Obligations would make the security boundary indirect and easier to omit.
4. **The PRN already stores its issuer context.** `PackagingRecyclingNote.registrationId` and `PackagingRecyclingNote.accreditation.id` are stored with the PRN. epr-backend can use that stored snapshot internally when it needs issuer or accreditation context; Waste Obligations should not reconstruct it from current organisation data.
5. **A resolver plus the existing endpoint would add avoidable coupling.** Waste Obligations would need one call to discover issuer hierarchy IDs and another to fetch the PRN, while depending on epr-backend's issuer route structure. A recipient-scoped lookup can perform one source-owned read using the two identifiers Waste Obligations actually has.
6. **A dedicated response contract is already needed.** The current detail response is missing fields required by the Waste Obligations schema. The new endpoint can expose the recipient-facing PRN detail contract deliberately rather than expanding an issuer journey endpoint for an unrelated consumer.

The new epr-backend endpoint should:

- accept the recipient `organisationId` and a string `prnId`;
- look up an RREPW/future PRN by its canonical epr-backend identity; mixed or legacy ID handling is only required if NPWD PRNs are later imported into epr-backend;
- return `404` when the PRN does not exist, is not visible to a recipient, or `prn.issuedToOrganisation.id` does not equal the route `organisationId`;
- avoid requiring issuer `organisationId`, `registrationId`, or `accreditationId` route values;
- use the registration and accreditation context stored on the PRN rather than deriving historical context from the issuer's current organisation record;
- return the fields needed by the common Waste Obligations schema, including the fields missing from the current `packagingRecyclingNoteById` response;
- return nullable accepted, rejected, and cancelled timestamps from the stored status projection within the common `audit` object, alongside the source-store audit timestamps;
- return `recipient.organisationId`, `displayName`, and the nullable recipient `name`, `tradingName`, and normalised `registrationType` fields so Waste Obligations can retain its own defensive route-scope check and expose the richer organisation snapshot epr-backend already stores;
- define recipient-visible status rules explicitly, including how pre-issue and soft-deleted states are handled;
- use service-to-service authentication and authorisation appropriate for Waste Obligations.

This endpoint is future work and does not block the initial common-backend-backed Waste Obligations implementation. It does block replacing or supplementing common backend with `epr-backend` as the PRN detail source.

### Current `epr-backend` Gaps

This is the gap analysis against the proposed Waste Obligations schema, using the current `packagingRecyclingNoteById` response as the baseline.

| Waste Obligations field | Current `epr-backend` position | Gap or action |
| --- | --- | --- |
| `id` | Current PRN domain has `id` and current get returns `id`. | Map epr-backend `id` to Waste Obligations `Prn.id` for RREPW/future PRNs. Type-compatible because Waste Obligations models PRN IDs as strings. |
| `number` | Current get returns `prnNumber`. | No gap, rename to `number`. |
| `type` | Domain has `isExport`; external mapper exposes `isExport`; current get returns `wasteProcessingType` but not `isExport`. RREPW payload has `isExport`. | Add `isExport` or a direct PRN/PERN `type` to the current get response, or define a reliable mapping from `wasteProcessingType`. |
| `status` | Current get returns `status` from the domain `status.currentStatus`, using `epr-backend` snake_case values. `epr-backend` does not have `rejected` as a current status; producer rejection moves the PRN to `awaiting_cancellation` while storing a rejected operation timestamp. | Normalise to the Waste Obligations PRN status vocabulary. Add `AwaitingCancellation` to the public vocabulary for source-faithful epr-backend/RREPW mapping. Keep `Rejected` for common-backend/RREPW rejected PRNs. Do not expose epr-backend `draft`, `deleted`, or `discarded` through this PRN detail contract. |
| `audit.acceptedAt` | Domain status projection and external mapper expose the accepted timestamp; current get does not return it. | Add it as nullable. |
| `audit.rejectedAt` | Domain status projection and external mapper expose the rejected operation timestamp; current get does not return it. | Add it as nullable, including when the current status is `AwaitingCancellation` or has advanced to `Cancelled`. |
| `audit.cancelledAt` | Domain status projection and external mapper expose the cancelled timestamp; current get does not return it. | Add it as nullable. |
| `issuedAt` | Current get returns `issuedAt`. | No data gap for issued PRNs; map directly. |
| `obligationYear` | Not present in the current PRN domain projection or current get response. It is also not present in the inspected RREPW payload, but is due to be added. | Missing from the current implementation. `epr-backend` will explicitly store `obligationYear` and provide it when Waste Obligations requests a PRN by ID. Do not rely on the current RREPR hard-coded `"2026"`. |
| `accreditationYear` | Current get returns `accreditationYear`. | No gap. |
| `decemberWaste` | Current get returns `isDecemberWaste`. | No gap, rename to `decemberWaste`. |
| `material` | Current get returns `material`; domain has accreditation material. | No data gap, but Waste Obligations must either receive PRN material values from `epr-backend` in the Waste Obligations vocabulary, or map `epr-backend` material codes/names to it. |
| `recyclingProcess` | Current get returns `processToBeUsed`. | No gap, rename to `recyclingProcess`. |
| `tonnage` | Current get returns `tonnage`. | No gap. |
| `issuer.organisationName` | Domain has `organisation.name`; external mapper exposes `issuedByOrganisation`; current get does not return it. RREPW payload has `issuedByOrganisation.name`. | Missing from current get response, but not missing from the inspected RREPW payload. Add issuer organisation details or expose them through a future detail contract. |
| `recipient.organisationId` | Domain and current get response have `issuedToOrganisation.id`. | No gap. Return it as the required recipient organisation identity, parse it as a GUID in the Waste Obligations adapter, and verify it matches the Waste Obligations route. |
| `recipient.displayName` | Current get returns `issuedToOrganisation`, including `name` and optional `tradingName` and `registrationType`. | Derive using the same rule as the current integration: for `ComplianceScheme`, prefer a non-blank `tradingName`; for `DirectProducer`, use `name`; when registration type is missing or unrecognised, prefer a non-blank `tradingName` and otherwise use `name`. |
| `recipient.name` | Current get returns `issuedToOrganisation.name`. | No gap, expose as a nullable string in the common schema because common backend and `legacy-prns` cannot distinguish it from their selected display value. |
| `recipient.tradingName` | Current get returns optional `issuedToOrganisation.tradingName`. | No gap, expose as a nullable string. |
| `recipient.registrationType` | Current get returns optional `issuedToOrganisation.registrationType`; current known values are `LARGE_PRODUCER` and `COMPLIANCE_SCHEME`. | Normalise to the existing Waste Obligations `RegistrationType` values: `LARGE_PRODUCER` -> `DirectProducer`, `COMPLIANCE_SCHEME` -> `ComplianceScheme`. Return `null` when missing or unrecognised rather than leaking upstream strings. |
| `authorisedBy.name` | Current get returns the signatory actor as `issuedBy.name`; the external RREPW mapper exposes the same actor as `status.authorisedBy.fullName`. | No gap for issued PRNs, map from `issuedBy.name` in the current get response or `status.authorisedBy.fullName` in an RREPW-shaped response. |
| `authorisedBy.position` | Current get returns the signatory actor as `issuedBy.position`; the external RREPW mapper exposes the same actor as `status.authorisedBy.jobTitle`. | No gap when source carries it, map from `issuedBy.position` in the current get response or `status.authorisedBy.jobTitle` in an RREPW-shaped response. |
| `reprocessingSite` | Domain accreditation has `siteAddress`; external mapper exposes `accreditation.siteAddress`; current get does not return it. RREPW payload has `accreditation.siteAddress`. | Missing from current get response, but not missing from the inspected RREPW payload. Add accreditation site address and format it for the Waste Obligations schema. |
| `accreditationNumber` | Domain accreditation has `accreditationNumber`; admin mapper exposes it; current get does not return it. RREPW payload has `accreditation.accreditationNumber`. | Missing from current get response, but not missing from the inspected RREPW payload. Add accreditation number to the current get response or future detail contract. |
| `reprocessorExporterAgency` | Domain accreditation has `submittedToRegulator`; current get does not return it. RREPW payload has `accreditation.submittedToRegulator`. | Missing from current get response, but not missing from the inspected RREPW payload. Add regulator details and map them to the Waste Obligations agency name. |
| `additionalNotes` | Current get returns `notes`. | No gap, rename to `additionalNotes`. |
| `audit.createdAt` | Current get returns `createdAt`; domain has `createdAt`. | No data gap. Convert to Waste Obligations `DateTimeOffset` format. |
| `audit.updatedAt` | Domain has `updatedAt`; current get does not return it. | Missing from current get response. Add `updatedAt` to the future detail contract. |

### Critique: Initial vs Future Integration

The initial common-backend-backed implementation is not blocked by the RREPW/epr-backend route issues. Waste Obligations can call common backend with only `organisationId` and `prnId`, using `X-EPR-ORGANISATION` plus `GET /api/v1/prn/{prnId}`.

The future RREPW/epr-backend integration is not adequately implementable until the required recipient-scoped endpoint is delivered. The inspected RREPW read contract is a list/sync endpoint, not a single-PRN endpoint. The inspected single-PRN endpoint is epr-backend `packagingRecyclingNoteById`, but it requires `registrationId` and `accreditationId`, which the Waste Obligations route does not have, and its `organisationId` is the issuer rather than the recipient organisation used by the Waste Obligations route.

The current epr-backend single-PRN endpoint is therefore not fit as a drop-in source for this Waste Obligations endpoint. The new endpoint must preserve the Waste Obligations recipient-organisation scope and verify `prn.issuedToOrganisation.id`, resolve any required issuer context internally, and expose the missing common-schema fields listed above.

On field availability, the inspected RREPW payload is close enough for the proposed common schema except for `obligationYear`, which is due to be added and must be returned as an integer. If the eventual RREPW/epr-backend detail endpoint cannot provide the same fields as the inspected list/sync payload, that endpoint should be treated as not fit for purpose and replaced or expanded before integration.

### Questions For The New `epr-backend` Recipient Endpoint

The need for a new recipient-scoped endpoint is a design conclusion rather than an open endpoint-selection question. These delivery and contract questions still need answering before RREPW/epr-backend can be integrated into the Waste Obligations PRN schema. They should not block the initial common-backend-backed implementation, but they do block treating the schema as source-complete across PRN services.

Decision already made: `Prn.id` is a string. Map common-backend `externalId`, `legacy-prns` `legacy.externalId`, and epr-backend `id` into that single field.

The only RREPW read route inspected so far is the Azure Function list/sync route, `GET v1/packaging-recycling-notes?statuses=...&dateFrom=...&dateTo=...&cursor=...`. The only existing epr-backend single-PRN read endpoint inspected so far is `packagingRecyclingNoteById`, and that route is issuer- and accreditation-scoped:

`GET /v1/organisations/{organisationId}/registrations/{registrationId}/accreditations/{accreditationId}/packaging-recycling-notes/{prnId}`

That shape is not directly callable from the proposed Waste Obligations route, because Waste Obligations has only the recipient `organisationId` and `prnId`.

1. Which team owns delivery of the new recipient-scoped epr-backend endpoint, and what will its final path, versioning, and service-to-service authentication contract be?
2. Which PRN statuses are visible to the recipient organisation? In particular, should `AwaitingAuthorisation` be hidden until the PRN/PERN is issued, alongside `draft`, `deleted`, and `discarded`?
3. Confirm that the RREPW-backed listing/navigation flow and the new recipient-scoped detail endpoint will expose the same epr-backend PRN `id`, so that identity remains stable throughout the new journey.
4. Can its response expose the common-schema fields that are present in the RREPW payload or epr-backend domain but missing from the current `packagingRecyclingNoteById` response: `isExport`/`type`, issuer organisation name, accreditation number, accreditation site address, reprocessor/exporter agency, accepted/rejected/cancelled timestamps, and `updatedAt`?
5. Confirm the delivery shape for `obligationYear` in `epr-backend`: it will be explicitly stored and returned by the PRN-by-ID response once RREPW supplies it.
6. Are PRN tonnages guaranteed to be whole numbers long term? The frontend/common-backend path is integer-based, but `epr-backend` storage allows numeric values in places.
7. If NPWD legacy PRNs are imported into `epr-backend`, will epr-backend support existing common-backend/legacy GUID IDs for those imported records, despite RREPW/future PRNs using ObjectId strings?
8. Will `issuedToOrganisation.registrationType` be constrained to `LARGE_PRODUCER` and `COMPLIANCE_SCHEME`, or can additional values appear? Waste Obligations will initially map only those two values and return `null` for an unrecognised value while still deriving `displayName` with the documented fallback.

## Specific PRN Frontend Pages

The packaging frontend renders a specific PRN/PERN through these routes, usually under the frontend base path such as `/report-data`.

| Route fragment | View or behaviour | Uses PRN detail |
| --- | --- | --- |
| `selected-prn/{id:guid}` | `Views/Prns/SelectSinglePrn.cshtml` | Main specific PRN/PERN detail page. |
| `download-selected-prn-pdf/{id:guid}` | Renders `SelectSinglePrn` as PDF | Same detail fields, plus PDF-only tonnage in words. |
| `accept-prn/{id:guid}` | `Views/PrnsAccept/AcceptSinglePrn.cshtml` | Accept confirmation for a single PRN/PERN. |
| `accepted-prn/{id:guid}` | `Views/PrnsAccept/AcceptedPrn.cshtml` | Accepted result page and detail view. |
| `download-accepted-prn-pdf/{id:guid}` | Renders `AcceptedPrn` as PDF | Same accepted detail fields, plus PDF-only tonnage in words. |
| `reject-prn/{id:guid}` | `Views/PrnsReject/RejectSinglePrn.cshtml` | Reject confirmation for a single PRN/PERN. |
| `rejected-prn/{id:guid}` | `Views/PrnsReject/RejectedPrn.cshtml` | Rejected result page and detail view. |
| `download-rejected-prn-pdf/{id:guid}` | Renders `RejectedPrn` as PDF | Same rejected detail fields, with rejected-PDF status wording override. |

The selected, accepted, and rejected detail pages share:

- `Views/Shared/Partials/Prns/_recyclingNoteStatus.cshtml`
- `Views/Shared/Partials/Prns/_recyclingNoteDetails.cshtml`
- `Views/Shared/Partials/Prns/_agenciesLogo.cshtml`

The logo partial uses static regulator images and does not need PRN data.

## Rendered Field Inventory

These are the fields currently rendered or needed to render/action a specific PRN/PERN page. This is the starting point for the new Waste Obligations PRN contract.

| UI content or behaviour | Current frontend value | Source field or fields | Current transform |
| --- | --- | --- | --- |
| PRN/PERN page title, headings, labels, and copy | `IsPrn`, `NoteType` | `isExport` | `false` -> `PRN`; `true` -> `PERN`. |
| PRN/PERN number | `PrnOrPernNumber` | `prnNumber` | Direct. |
| Specific-note identity in links, hidden form fields, and PDF routes | `ExternalId` | `externalId` | Direct. |
| Issue year | `IssueYear` | `issueDate` | `DateIssued.Year`. |
| Date issued row | `DateIssuedDisplay` | `issueDate` | `dd MMM yyyy` in current culture. |
| Status tag | `ApprovalStatus` | `prnStatus` | `AWAITINGACCEPTANCE` -> `AWAITING ACCEPTANCE`; `CANCELED` -> `CANCELLED`; otherwise current value. |
| Status meaning text | `ApprovalStatusExplanationTranslation` | `prnStatus`, `isExport` | Translation key derived from note type and status. |
| Status tag colour | `ApprovalStatusDisplayCssColour` | `prnStatus` | Awaiting acceptance grey, accepted green, cancelled yellow, rejected red. |
| Rejected PDF status wording | `ApprovalStatus`, `IsPrn` | `prnStatus`, `isExport` | Rejected PDFs display cancellation-specific wording. |
| Issued by row | `IssuedBy` | `issuedByOrg` | Direct. |
| Reprocessing site row | `ReproccessingSiteAddress` | `reprocessingSite`, `isExport` | Rendered only for PRNs. |
| Authorised by row | `AuthorisedBy` | `prnSignatory` | Direct. |
| Position row | `Position` | `prnSignatoryPosition` | Null maps to empty string. |
| Accreditation number row | `AccreditationNumber` | `accreditationNumber` | Direct. |
| December waste row | `DecemberWasteDisplay` | `decemberWaste` | `true` -> yes; `false` -> no. |
| December waste warning | `IsDecemberWaste`, `ObligationYear`, `IsPrn` | `decemberWaste`, `obligationYear`, `isExport` | Warning rendered only when December waste is true. |
| Material row | `Material` | `materialName`, `issueDate` | Material value is localised; localisation resource set changes after the configured fibre launch date. |
| Material group copy | `MaterialGroup` | `materialName` | `Fibre` maps to `Paper/board`; other materials pass through. |
| Recycling process row | `RecyclingProcess` | `processToBeUsed` | Null maps to empty string, then displayed via PRN data localisation. |
| Tonnage row and accept/result copy | `Tonnage` | `tonnageValue` | Direct integer. |
| PDF tonnage in words | `Tonnage` | `tonnageValue` | Converted to words by the frontend for PDFs. |
| Packaging producer or compliance scheme row | `NameOfProducerOrComplianceScheme` | `organisationName` | Direct. |
| Additional note row | `AdditionalNotes` | `issuerNotes` | Empty or whitespace displays "not provided". |
| Accept/reject button visibility | `IsStatusEditable` | `prnStatus`, `obligationYear`, `decemberWaste` | Editable only when awaiting acceptance and at least one acceptance year is available. |
| Accept confirmation heading and accepted result copy | `EffectiveAcceptanceYear` | `obligationYear`, `decemberWaste` | Derived from available acceptance years and current compliance year. |

## Derived Values Owned By The Frontend Today

The new endpoint should provide the raw values needed to derive these fields. It does not need to return these display-only values in the first version unless ownership of display rules moves to Waste Obligations.

| Derived value | Inputs needed from API |
| --- | --- |
| `IsPrn` and `NoteType` | `type` or upstream `isExport`. |
| `IssueYear` | `issuedAt`. |
| `DateIssuedDisplay` | `issuedAt`. |
| `DecemberWasteDisplay` | `decemberWaste`. |
| `ApprovalStatusExplanationTranslation` | `type`, `status`. |
| `ApprovalStatusDisplayCssColour` | `status`. |
| `MaterialGroup` | `material`. |
| Localised material display | `material`, `issuedAt`. |
| `AvailableAcceptanceYears`, `IsStatusEditable`, `EffectiveAcceptanceYear` | `status`, `obligationYear`, `decemberWaste`, current compliance year. |
| PDF tonnage in words | `tonnage`. |

## Frontend-Owned Acceptance Fields

`AvailableAcceptanceYears`, `IsStatusEditable`, and `EffectiveAcceptanceYear` are frontend concerns and should not be added to the Waste Obligations PRN API response for the initial endpoint. The API should provide only the raw inputs needed by the frontend: `status`, `obligationYear`, and `decemberWaste`. The current frontend also uses the current UK compliance year, derived from the current date/time.

| Frontend field | Current logic | Source links |
| --- | --- | --- |
| `AvailableAcceptanceYears` | Derived during PRN model mapping. If `obligationYear` cannot be parsed, return `[]`. If the PRN year is greater than the current compliance year, return `[]`. For non-December-waste PRNs, return `[prnYear]` only when the PRN year equals the current compliance year. For December-waste PRNs, 2025 has special handling and returns only the current compliance year for compliance years 2025 or 2026. Other December-waste PRNs return `[prnYear, prnYear + 1]` before 1 February of the following year, `[currentComplianceYear]` when the following compliance year is current, and `[]` after expiry. | `epr-packaging-frontend`: [resolver](https://github.com/DEFRA/epr-packaging-frontend/blob/main/src/FrontendSchemeRegistration.UI/Mappers/PrnAvailableAcceptanceYearsResolver.cs#L20-L63), [mapper assignment](https://github.com/DEFRA/epr-packaging-frontend/blob/main/src/FrontendSchemeRegistration.UI/Mappers/PrnModelMapper.cs#L13-L18), [resolver test cases](https://github.com/DEFRA/epr-packaging-frontend/blob/main/src/FrontendSchemeRegistration.UI.UnitTests/Mappers/PrnAvailableAcceptanceYearsResolverTests.cs#L14-L50). |
| `IsStatusEditable` | `true` only when `ApprovalStatus == PrnStatus.AwaitingAcceptance` and `AvailableAcceptanceYears.Length > 0`. Used to show/hide accept/reject actions. | `epr-packaging-frontend`: [view model property](https://github.com/DEFRA/epr-packaging-frontend/blob/main/src/FrontendSchemeRegistration.UI/ViewModels/Prns/BasePrnViewModel.cs#L37-L40), [specific PRN buttons](https://github.com/DEFRA/epr-packaging-frontend/blob/main/src/FrontendSchemeRegistration.UI/Views/Prns/SelectSinglePrn.cshtml#L55-L78), [accept confirmation button](https://github.com/DEFRA/epr-packaging-frontend/blob/main/src/FrontendSchemeRegistration.UI/Views/PrnsAccept/AcceptSinglePrn.cshtml#L20-L26), [reject confirmation button](https://github.com/DEFRA/epr-packaging-frontend/blob/main/src/FrontendSchemeRegistration.UI/Views/PrnsReject/RejectSinglePrn.cshtml#L18-L24). |
| `EffectiveAcceptanceYear` | Returns the minimum available acceptance year as a string, or an empty string when there are no available acceptance years. This is currently a UI workaround because multiple acceptance-year choice is not implemented. | `epr-packaging-frontend`: [view model property](https://github.com/DEFRA/epr-packaging-frontend/blob/main/src/FrontendSchemeRegistration.UI/ViewModels/Prns/PrnViewModel.cs#L66-L74), [accept confirmation heading](https://github.com/DEFRA/epr-packaging-frontend/blob/main/src/FrontendSchemeRegistration.UI/Views/PrnsAccept/AcceptSinglePrn.cshtml#L6-L9), [accepted result heading](https://github.com/DEFRA/epr-packaging-frontend/blob/main/src/FrontendSchemeRegistration.UI/Views/PrnsAccept/AcceptedPrn.cshtml#L23-L26). |
| Current compliance year | January is treated as the previous compliance year; February to December use the current calendar year, after converting to UK time. | `epr-packaging-frontend`: [compliance year helper](https://github.com/DEFRA/epr-packaging-frontend/blob/main/src/FrontendSchemeRegistration.Application/Extensions/DateTimeExtensions.cs#L36-L47). |

## Common Material And Status Values

The existing Waste Obligations obligations endpoint already publishes explicit material and status vocabularies in the public API contract. `Obligation.Material` uses `PossibleValue` attributes for `Plastic`, `Glass`, `Aluminium`, `Steel`, `Wood`, `GlassRemelt`, and `Paper`; `Obligation.Status` uses `NoDataYet`, `Met`, and `NotMet`. The OpenAPI schema transformer turns those attributes into enum values in the generated contract.

The obligations endpoint maps PRN common backend `materialName` directly into `Obligation.material`. For obligations, the expected material names are therefore:

```text
Plastic
Glass
Aluminium
Steel
Wood
GlassRemelt
Paper
```

I inspected `epr-prn-common-backend` to confirm there are no additional material names returned by the obligation calculation endpoint. The seeded material table includes an internal `FibreComposite` material, but `GetObligationCalculation` deliberately combines `Paper` and `FibreComposite` obligation rows into a single `Paper` response row. The unit tests assert that `FibreComposite` is not present in the returned obligation data. Glass is returned as two common values: `Glass` for remaining glass and `GlassRemelt` for glass remelt.

For PRN detail, Waste Obligations should use a common API material language rather than passing through upstream display/source material strings. The PRN material vocabulary should be the existing obligation material values plus `Fibre`, because a specific PRN can be fibre and the frontend needs to preserve that distinction:

```text
Plastic
Glass
Aluminium
Steel
Wood
GlassRemelt
Paper
Fibre
```

The common-backend PRN detail endpoint returns `PrnDto.materialName`, copied from persisted `Eprn.MaterialName`. That source value can be an RPD/RREPW material name or a legacy NPWD material name, so Waste Obligations should map it before returning `material`:

| PRN common backend `materialName` | Waste Obligations `material` |
| --- | --- |
| `Aluminium` | `Aluminium` |
| `Plastic` | `Plastic` |
| `Steel` | `Steel` |
| `Wood` | `Wood` |
| `Wood Composting` | `Wood` |
| `Paper/board` | `Paper` |
| `Paper Composting` | `Paper` |
| `Fibre` | `Fibre` |
| `Glass Other` | `Glass` |
| `Glass Re-melt` | `GlassRemelt` |

When PRNs are read directly from RREPW or from an `epr-backend` projection of the RREPW contract, use the same Waste Obligations material vocabulary. The RREPW material values inspected in `epr-prn-integration-function` are `aluminium`, `fibre`, `glass`, `paper`, `plastic`, `steel`, and `wood`. Glass needs `glassRecyclingProcess` to distinguish remaining glass from remelt glass:

| RREPW `accreditation.material` | RREPW `accreditation.glassRecyclingProcess` | Current sync value sent to common backend | Waste Obligations `material` |
| --- | --- | --- | --- |
| `aluminium` | n/a | `Aluminium` | `Aluminium` |
| `fibre` | n/a | `Fibre` | `Fibre` |
| `glass` | `glass_other` | `Glass Other` | `Glass` |
| `glass` | `glass_re_melt` | `Glass Re-melt` | `GlassRemelt` |
| `paper` | n/a | `Paper/board` | `Paper` |
| `plastic` | n/a | `Plastic` | `Plastic` |
| `steel` | n/a | `Steel` | `Steel` |
| `wood` | n/a | `Wood` | `Wood` |

If RREPW sends `glass` without a recognised `glassRecyclingProcess`, the current integration mapper returns `null` for common-backend `materialName`. A future direct RREPW/epr-backend integration should treat that as an unmapped source value and should not expose it through `material`.

This mapping has been sanity checked against the current `epr-prn-integration-function` Azure Function flow: `FetchRrepwIssuedPrnsFunction` fetches RREPW PRNs, calls `RrepwMappers.Map`, sets `SavePrnDetailsRequest.MaterialName` from `ConvertMaterialToEprnMaterial`, and posts the request to common backend `api/v2/prn`. The current mapper tests assert the RREPW-to-common-backend material names shown in the table above.

RREPW currently maps source `fibre` to common-backend `Fibre`, so `Fibre` must not be collapsed to `Paper` for the PRN detail endpoint. Fibre only collapses into `Paper` in the obligation calculation response because the obligation calculation combines the internal `FibreComposite` obligation bucket with `Paper`.

If common backend returns a PRN `materialName` that is not in this table, Waste Obligations should not pass it through to the public API. Treat it as an integration/data-quality failure, log the source value, and add an explicit mapping before exposing it.

Do not add `materialCode` to the initial PRN detail schema. The API `material` value itself should be the stable public value. If the frontend still needs the original PRN evidence material string for display during migration, add it separately as a clearly named field such as `evidenceMaterial`; do not overload `material` with upstream-specific strings.

When `epr-packaging-frontend` moves to this endpoint, its PRN material localisation should use the Waste Obligations vocabulary. It already has resource keys for `Paper` and `Fibre`; it will need to support the canonical `GlassRemelt` value, or explicitly map `GlassRemelt` to the existing glass-remelt display resource.

Status should follow the existing Waste Obligations style: return normalised API values, not display-ready labels. The PRN detail schema should expose status as the normalised values listed in its `PossibleValue` attributes and leave localisation, CSS class selection, and explanatory copy to the frontend.

The common PRN status vocabulary should be:

```text
AwaitingAuthorisation
AwaitingAcceptance
Accepted
Rejected
AwaitingCancellation
Cancelled
```

The first common-backend-backed implementation will only return `AwaitingAcceptance`, `Accepted`, `Rejected`, and `Cancelled`, because those are the only PRN common backend evidence statuses. The wider six-value vocabulary is needed for future RREPW/epr-backend compatibility.

Do not include epr-backend `draft`, `deleted`, or `discarded` in the Waste Obligations PRN detail vocabulary. Those are epr-backend creation/soft-delete lifecycle states, not RREPW/common-backend issued-evidence states. If the future epr-backend integration encounters those states for this endpoint, Waste Obligations should treat the PRN as not visible through the PRN detail contract, most likely by returning `404`.

Status mapping:

| Source | Source status | Waste Obligations `status` |
| --- | --- | --- |
| PRN common backend | `AWAITINGACCEPTANCE` | `AwaitingAcceptance` |
| PRN common backend | `ACCEPTED` | `Accepted` |
| PRN common backend | `REJECTED` | `Rejected` |
| PRN common backend | `CANCELLED` / `CANCELED` | `Cancelled` |
| RREPW | `awaiting_authorisation` | `AwaitingAuthorisation` |
| RREPW | `awaiting_acceptance` | `AwaitingAcceptance` |
| RREPW | `accepted` | `Accepted` |
| RREPW | `rejected` | `Rejected` |
| RREPW | `awaiting_cancellation` | `AwaitingCancellation` |
| RREPW | `cancelled` | `Cancelled` |
| epr-backend / epr-frontend | `awaiting_authorisation` | `AwaitingAuthorisation` |
| epr-backend / epr-frontend | `awaiting_acceptance` | `AwaitingAcceptance` |
| epr-backend / epr-frontend | `accepted` | `Accepted` |
| epr-backend / epr-frontend | `awaiting_cancellation` | `AwaitingCancellation` |
| epr-backend / epr-frontend | `cancelled` | `Cancelled` |
| epr-backend | `draft`, `deleted`, `discarded` | Do not expose through this endpoint. |

The `Rejected`/`AwaitingCancellation` distinction is now understood rather than an open question:

- In the current common-backend path, producer rejection is persisted as common-backend status `REJECTED`.
- `epr-prn-integration-function` sends common-backend `REJECTED` PRNs back to RREPW by posting to `v1/packaging-recycling-notes/{prnNumber}/reject` with a `rejectedAt` timestamp.
- RREPW has a valid `rejected` status.
- In epr-backend, producer rejection is a business operation/event that records `status.rejected`/`rejectedAt`, but the current PRN status becomes `awaiting_cancellation`. The epr-frontend then displays and acts on `awaiting_cancellation`, and final cancellation moves the status to `cancelled`.

Therefore the common schema should not collapse `AwaitingCancellation` into `Rejected`. Use `Rejected` only when the source current status is actually rejected, and use `AwaitingCancellation` when the source current status is awaiting cancellation.

Source links:

| Repository | Relevant source |
| --- | --- |
| `waste-obligations` | [`Obligation.Material` and `Obligation.Status` possible values](https://github.com/DEFRA/waste-obligations/blob/main/src/Api/Dtos/Obligation.cs#L10-L37), [`Material` constants](https://github.com/DEFRA/waste-obligations/blob/main/src/Api/Dtos/Material.cs#L3-L13), [`ObligationStatus` constants](https://github.com/DEFRA/waste-obligations/blob/main/src/Api/Dtos/ObligationStatus.cs#L3-L10), [`PossibleValueSchemaTransformer`](https://github.com/DEFRA/waste-obligations/blob/main/src/Api/Endpoints/OpenApi/PossibleValueSchemaTransformer.cs#L10-L41), [`Obligation.material` mapper](https://github.com/DEFRA/waste-obligations/blob/main/src/Api/Services/PrnCommonBackend/Mappers.cs#L7-L21). |
| `epr-prn-common-backend` | [`MaterialType` enum used by obligation calculation](https://github.com/DEFRA/epr-prn-common-backend/blob/feature/MO-354/src/EPR.PRN.Backend.API.Common/Enums/MaterialType.cs#L3-L14), [`PrnConstants.Materials` source material strings](https://github.com/DEFRA/epr-prn-common-backend/blob/feature/MO-354/src/EPR.PRN.Backend.API.Common/Constants/PrnConstants.cs#L57-L69), [`PRN material mapping seed data`](https://github.com/DEFRA/epr-prn-common-backend/blob/feature/MO-354/src/EPR.PRN.Backend.Data/EprContext.cs#L140-L165), [`Fibre` mapping migration](https://github.com/DEFRA/epr-prn-common-backend/blob/feature/MO-354/src/EPR.PRN.Backend.Data/Migrations/20260226162853_AddFibreMaterialMapping.cs#L13-L19), [`Paper` and `FibreComposite` obligation combination](https://github.com/DEFRA/epr-prn-common-backend/blob/feature/MO-354/src/EPR.PRN.Backend.Obligation/Services/ObligationCalculatorService.cs#L187-L207), [`PrnDto` material copy from `Eprn.MaterialName`](https://github.com/DEFRA/epr-prn-common-backend/blob/feature/MO-354/src/EPR.PRN.Backend.API/Dto/PrnBaseDto.cs#L71-L84). |
| `epr-prn-integration-function` | [`FetchRrepwIssuedPrnsFunction` calls `RrepwMappers.Map` and saves the mapped PRN](https://github.com/DEFRA/epr-prn-integration-function/blob/main/src/EprPrnIntegration.Api/Functions/FetchRrepwIssuedPrnsFunction.cs#L112-L167), [`PrnService.SavePrn` posts to `api/v2/prn`](https://github.com/DEFRA/epr-prn-integration-function/blob/main/src/EprPrnIntegration.Common/RESTServices/PrnBackendService/PrnService.cs#L32-L39), [`RREPW` status constants](https://github.com/DEFRA/epr-prn-integration-function/blob/main/src/EprPrnIntegration.Common/Models/Rrepw/RrepwStatus.cs#L3-L11), [`RREPW` material constants](https://github.com/DEFRA/epr-prn-integration-function/blob/main/src/EprPrnIntegration.Common/Models/Rrepw/RrepwMaterialName.cs#L6-L15), [`RREPW` glass recycling process constants](https://github.com/DEFRA/epr-prn-integration-function/blob/main/src/EprPrnIntegration.Common/Models/Rrepw/RrepwGlassRecyclingProcess.cs#L6-L10), [`RREPW` material to common-backend PRN detail material mapping](https://github.com/DEFRA/epr-prn-integration-function/blob/main/src/EprPrnIntegration.Common/Mappers/RrepwMappers.cs#L139-L158), [`RREPW` material mapping tests](https://github.com/DEFRA/epr-prn-integration-function/blob/main/src/EprPrnIntegration.Common.UnitTests/Mappers/RrepwMappersTests.cs#L90-L118), [`RREPW` stub all-material test](https://github.com/DEFRA/epr-prn-integration-function/blob/main/src/EprPrnIntegration.Common.UnitTests/RESTServices/RrepwService/StubbedRrepwServiceTests.cs#L240-L255), [`RREPW accept/reject outbound mapper`](https://github.com/DEFRA/epr-prn-integration-function/blob/main/src/EprPrnIntegration.Common/RESTServices/RrepwService/RrepwService.cs#L130-L154). |
| `epr-backend` | [`PRN status constants and transitions`](https://github.com/DEFRA/epr-backend/blob/main/src/packaging-recycling-notes/domain/model.js#L9-L70), [`external reject endpoint maps rejection to awaiting cancellation`](https://github.com/DEFRA/epr-backend/blob/main/src/packaging-recycling-notes/routes/reject.js#L32-L36), [`ledger rejected event projects to awaiting cancellation but stores rejected slot`](https://github.com/DEFRA/epr-backend/blob/main/src/packaging-recycling-notes/application/fold-prn-from-tail-events.js#L9-L32), [`external PRN mapper exposes currentStatus and rejectedAt`](https://github.com/DEFRA/epr-backend/blob/main/src/packaging-recycling-notes/application/external-prn-mapper.js#L18-L38). |
| `epr-frontend` | [`PRN status constants`](https://github.com/DEFRA/epr-frontend/blob/main/src/server/common/constants/statuses.js#L1-L8), [`PRN list status grouping`](https://github.com/DEFRA/epr-frontend/blob/main/src/server/prns/list-controller.js#L64-L78), [`PRN status display mapping`](https://github.com/DEFRA/epr-frontend/blob/main/src/server/prns/helpers/get-status-config.js#L14-L38), [`awaiting-cancellation confirm flow`](https://github.com/DEFRA/epr-frontend/blob/main/src/server/prns/cancel-controller.js#L20-L78), [`cancelled success guard`](https://github.com/DEFRA/epr-frontend/blob/main/src/server/prns/cancelled-controller.js#L14-L31). |
| `epr-packaging-frontend` | [`Fibre` material group behaviour](https://github.com/DEFRA/epr-packaging-frontend/blob/main/src/FrontendSchemeRegistration.UI/ViewModels/Prns/BasePrnViewModel.cs#L13-L17), [`PRN material localiser`](https://github.com/DEFRA/epr-packaging-frontend/blob/main/src/FrontendSchemeRegistration.UI/Resources/PrnDataResourcesLocalizer.cs#L18-L21), [`Paper` and `Fibre` material resources](https://github.com/DEFRA/epr-packaging-frontend/blob/main/src/FrontendSchemeRegistration.UI/Resources/PrnDataResources.en.resx#L141-L190). |

## Proposed Waste Obligations Schema

Add a new API DTO named `Prn` under `src/Api/Dtos`.

The first response shape should contain the specific PRN page baseline plus non-rendered `legacy-prns` root fields that RREPW can already supply. It should not include non-rendered fields that only common backend or `legacy-prns` can currently supply. It may include nullable source-enrichment fields where epr-backend has a richer, stable snapshot and the other sources can represent absence explicitly.

```json
{
  "id": "0d2f531d-0213-494b-8c8b-4133051bd44f",
  "number": "PRN123",
  "type": "PRN",
  "status": "AwaitingAcceptance",
  "issuedAt": "2025-06-15T10:30:00+00:00",
  "obligationYear": 2025,
  "accreditationYear": 2025,
  "decemberWaste": false,
  "material": "Aluminium",
  "recyclingProcess": "R3",
  "tonnage": 999,
  "issuer": {
    "organisationName": "Acme Reprocessors Ltd"
  },
  "recipient": {
    "organisationId": "0d2f531d-0213-494b-8c8b-4133051bd44e",
    "displayName": "Test Producer Ltd",
    "name": null,
    "tradingName": null,
    "registrationType": null
  },
  "authorisedBy": {
    "name": "Jane Smith",
    "position": "Director"
  },
  "accreditationNumber": "ACC123",
  "reprocessingSite": "42 Factory Road, Manchester",
  "reprocessorExporterAgency": "Environment Agency",
  "additionalNotes": "Important note about this PRN",
  "audit": {
    "createdAt": "2026-01-15T10:00:00+00:00",
    "updatedAt": "2026-01-15T10:00:00+00:00",
    "acceptedAt": null,
    "rejectedAt": null,
    "cancelledAt": null
  }
}
```

### Schema Notes

- `id` is an opaque string containing the stable identity for the pool that served the PRN. The initial common-backend adapter maps its `externalId`; `legacy-prns` preserves that GUID for NPWD PRNs; the new RREPW/epr-backend journey maps the epr-backend PRN `id`. This is not an ID migration: each new listing/navigation flow must pass the canonical ID for its own pool into the detail route. The field is not the common-backend SQL integer `id`.
- `sourceSystemId` is excluded from the Waste Obligations schema. It is a current common-backend cache segmentation/provenance detail and should not be required by frontend consumers or future epr-backend APIs.
- `number` is the existing PRN/PERN evidence number.
- `type` is derived from `isExport`: `PRN` when `false`, `PERN` when `true`.
- `status` should be normalised to a Waste Obligations enum/string set and exposed with `PossibleValue` attributes: `AwaitingAuthorisation`, `AwaitingAcceptance`, `Accepted`, `Rejected`, `AwaitingCancellation`, and `Cancelled`. Do not return display labels from the API.
- `issuedAt` should be a required `DateTimeOffset`. The `*At` suffix makes clear that this is a timestamp and aligns it with the other temporal properties. It should have a property-level DTO description comment stating that it is the time the PRN/PERN was issued and, for RREPW/epr-backend records, is also the time it was authorised. The comment should identify the source mapping from RREPW `status.authorisedAt` and epr-backend `status.issued.at`, and must be emitted as the OpenAPI property description.
- `material` should use the Waste Obligations PRN material vocabulary and be exposed with `PossibleValue` attributes: `Plastic`, `Glass`, `Aluminium`, `Steel`, `Wood`, `GlassRemelt`, `Paper`, and `Fibre`. Do not pass upstream-specific strings such as `Paper/board`, `Glass Other`, or `Glass Re-melt` through as `material`.
- `tonnage` remains an integer because upstream common backend and the current frontend model both use `int`.
- `obligationYear` should be a required integer in the Waste Obligations schema. The first common-backend-backed mapper must parse the upstream string to an integer. If the upstream value is missing, blank, or not an integer, throw a data-quality exception so the endpoint fails rather than returning a partial PRN contract.
- `accreditationYear` should be a nullable integer. It is not rendered by the current specific PRN/PERN UI, but it is a root field in `legacy-prns` and is available from RREPW accreditation data.
- `reprocessorExporterAgency` should be a nullable string. It is not rendered by the current specific PRN/PERN UI, but it is a root field in `legacy-prns` and can be mapped from the RREPW accreditation regulator.
- `audit.createdAt`, `audit.updatedAt`, `audit.acceptedAt`, `audit.rejectedAt`, and `audit.cancelledAt` are included as nullable `DateTimeOffset` values for administrative use, even though the current frontend does not render them. The created/updated fields have source-store semantics; the accepted/rejected/cancelled fields represent PRN lifecycle events. `issuedAt` already carries the RREPW/epr-backend authorised/issued timestamp, so do not duplicate it as `audit.authorisedAt`. Public JSON should use Waste Obligations' existing ISO 8601 extended format with offset, for example `2026-01-15T10:00:00+00:00`. Do not pass through upstream `DateTime`/JavaScript `Date` strings directly. DTO/OpenAPI descriptions must explain each field's semantics, source-specific availability, and when `null` is returned.
- `recyclingProcess`, `authorisedBy.name`, `authorisedBy.position`, `reprocessingSite`, and `additionalNotes` should be nullable strings. The frontend can continue applying empty-string or "not provided" fallbacks.
- `reprocessingSite` is only rendered by the current frontend for PRNs, but it can remain present and nullable in the schema for both note types.
- `issuer` identifies the organisation that issued the PRN/PERN. The role noun distinguishes it from `authorisedBy`, which identifies a person, while avoiding the similar-looking `issuedBy` and `authorisedBy` property names.
- `recipient.organisationId` should be a required `Guid`. It repeats the organisation-scoped route value deliberately so Waste Obligations can verify the source response before returning it. Common backend supplies `organisationId`, `legacy-prns` preserves it as `Organisation.Id`, and RREPW/epr-backend supplies `issuedToOrganisation.id`. A future epr-backend adapter must parse its string value as a GUID and treat a missing or invalid value as a data-quality failure.
- `recipient.displayName` should be a required string and is the only recipient name a consumer should need for rendering. Common backend and `legacy-prns` map their already-selected `organisationName`/`Organisation.Name` into it. For epr-backend, derive it from the stored recipient snapshot: for `ComplianceScheme`, prefer a non-blank `tradingName`; for `DirectProducer`, use `name`; when registration type is missing or unrecognised, prefer a non-blank `tradingName` and otherwise use `name`. Treat a missing or blank result as a data-quality failure.
- `recipient.name`, `recipient.tradingName`, and `recipient.registrationType` should be nullable source-enrichment fields. epr-backend stores these recipient details, but common backend and `legacy-prns` only retain the selected display name and must return `null` for all three. Normalise epr-backend `LARGE_PRODUCER` to the existing Waste Obligations `DirectProducer` value and `COMPLIANCE_SCHEME` to `ComplianceScheme`; return `null` for a missing or unrecognised source value.
- Any nullable public DTO property that can only be populated by one source integration must have a property-level DTO description comment. The description should identify which source supplies the value, explain why other sources return `null`, and describe the value's semantics rather than its implementation or storage details. Ensure the comment is emitted as the property's OpenAPI description so generated API documentation makes the source-dependent nullability explicit.
- Non-rendered upstream fields should not be added to the first public schema unless they are first-class `legacy-prns` root fields and RREPW can also supply them. `obligationYear` is the exception: it is not in the inspected RREPW payload yet, but it is rendered/used by the frontend and is due to be added as a required integer. Nullable audit dates are a second exception because they are documented as source-store audit dates and nullable for sources that cannot supply them. The nullable recipient organisation details are a third, deliberate exception: epr-backend already snapshots stable name, trading-name, and registration-type values on the PRN, while older sources can honestly represent their absence.
- Exclude `producerAgency`, `signature`, and `packagingProducer` for now. They are root fields in `legacy-prns`, but they are not rendered today and are not available as source PRN values from the inspected RREPW payload/model.
- The schema must remain source-compatible with the possible future NPWD legacy source, `legacy-prns`, and the future RREPW/epr-backend source. Before any new public field is added, confirm that it exists in the current `legacy-prns` Mongo document, can be supplied by RREPW/epr-backend, or record the source change required to supply it.
- `legacy-prns` can supply the included fields for NPWD PRNs from its current Mongo document shape, but a read API/direct integration still needs to be designed before it can serve NPWD legacy PRNs for this endpoint. Its public PRN identity should be `legacy.externalId`.
- The fields currently missing from the `epr-backend` `packagingRecyclingNoteById` response are `type`/`isExport`, `obligationYear`, `issuer.organisationName`, `reprocessingSite`, `accreditationNumber`, `reprocessorExporterAgency`, `audit.acceptedAt`, `audit.rejectedAt`, `audit.cancelledAt`, and `audit.updatedAt`. `obligationYear` is a known planned addition: `epr-backend` will store it explicitly and return it from the PRN-by-ID response. These fields, recipient scoping, PRN identity, and status semantics must be delivered through the required recipient-scoped epr-backend detail endpoint before `epr-backend` can replace or sit beside common backend as the detail source.

### Recipient Organisation Implementation Rationale

`displayName` and `name` must remain separate. The current integration chooses a display value before saving to common backend: a large producer uses its name, a compliance scheme prefers its trading name, and the fallback prefers trading name when present. Common backend and the migrated `legacy-prns` document retain only that selected value. Mapping it into both `displayName` and `name` would invent legal-name semantics that the source no longer proves.

The common-backend and `legacy-prns` adapters should therefore map their selected organisation value only to required `recipient.displayName`, and set `recipient.name`, `recipient.tradingName`, and `recipient.registrationType` to `null`. The epr-backend adapter should map the stored recipient snapshot into all four recipient fields and derive `displayName` centrally using the rule above. Frontends should render `recipient.displayName` directly and should not reimplement organisation-type selection.

The new PRN DTO must declare `recipient.registrationType` as nullable `Defra.WasteObligations.Api.Dtos.RegistrationType`; do not introduce a PRN-specific registration-type enum. This reuses the existing Waste Obligations JSON values `DirectProducer` and `ComplianceScheme`. Keep the epr-backend string conversion inside its adapter so upstream values do not become part of the public contract. An unrecognised source registration type should result in nullable `recipient.registrationType` plus the documented display-name fallback; it should not make an otherwise renderable PRN unreadable.

## Field Mapping

| Waste Obligations field | PRN common backend field | Transform |
| --- | --- | --- |
| `id` | `externalId` | Format the upstream GUID as a string. Do not expose it as a `Guid`/`uuid` schema field. |
| `number` | `prnNumber` | Direct. |
| `type` | `isExport` | `true` -> `PERN`, `false` -> `PRN`. |
| `status` | `prnStatus` or `prnStatusId` | Prefer `prnStatus`; normalise common-backend statuses into the Waste Obligations PRN status vocabulary. For the initial source this means `AWAITINGACCEPTANCE` -> `AwaitingAcceptance`, `ACCEPTED` -> `Accepted`, `REJECTED` -> `Rejected`, and `CANCELLED`/`CANCELED` -> `Cancelled`. |
| `audit.acceptedAt` | Not available separately from the detail endpoint. | Return `null`. |
| `audit.rejectedAt` | Not available separately from the detail endpoint. | Return `null`. |
| `audit.cancelledAt` | Not available separately from the detail endpoint. | Return `null`. |
| `issuedAt` | `issueDate` | Convert to `DateTimeOffset` or preserve as a UTC-compatible instant. |
| `obligationYear` | `obligationYear` | Parse string to required int. Throw a data-quality exception when the value is missing, blank, or not an integer. |
| `accreditationYear` | `accreditationYear` | Parse string to nullable int. |
| `decemberWaste` | `decemberWaste` | Direct. |
| `material` | `materialName` | Map upstream source/evidence material string into the Waste Obligations PRN material vocabulary. |
| `recyclingProcess` | `processToBeUsed` | Direct nullable string. |
| `tonnage` | `tonnageValue` | Direct. |
| `issuer.organisationName` | `issuedByOrg` | Direct. |
| `recipient.organisationId` | `organisationId` | Direct. Require it to match the route `organisationId`. |
| `recipient.displayName` | `organisationName` | Direct. The common-backend value is already selected for display by the integration or NPWD source. |
| `recipient.name` | Not available separately. | Return `null`. Do not duplicate `organisationName`, because its legal-name semantics are not guaranteed. |
| `recipient.tradingName` | Not available separately. | Return `null`. |
| `recipient.registrationType` | Not available. | Return `null`. |
| `authorisedBy.name` | `prnSignatory` | Direct nullable string. |
| `authorisedBy.position` | `prnSignatoryPosition` | Direct nullable string. |
| `reprocessingSite` | `reprocessingSite` | Direct nullable string. |
| `accreditationNumber` | `accreditationNumber` | Direct. |
| `reprocessorExporterAgency` | `reprocessorExporterAgency` | Direct nullable string. |
| `additionalNotes` | `issuerNotes` | Direct nullable string. |
| `audit.createdAt` | `createdOn` | Convert common-backend `DateTime` to UTC `DateTimeOffset`. |
| `audit.updatedAt` | `lastUpdatedDate` | Convert common-backend `DateTime` to UTC `DateTimeOffset`. |

## Endpoint Behaviour

The endpoint should follow the existing organisation endpoint pattern:

1. Require `PolicyNames.Read`.
2. Bind `organisationId` from the route as `Guid` and `prnId` from the route as `string`.
3. Read the organisation using `IWasteOrganisationsService.Read(organisationId, cancellationToken)`.
4. Read the PRN using `IPrnCommonBackendService.ReadPrn(organisationId, prnId, cancellationToken)`.
5. Run both reads concurrently where practical.
6. Return `404` when the organisation is not found.
7. Return `404` when PRN common backend returns `404` or `null`.
8. Return `404` if `prn.recipient.organisationId` does not match the route `organisationId`.
9. Return `200` with the new `Prn` DTO otherwise.

The extra organisation lookup keeps behaviour aligned with existing organisation-scoped endpoints, even though the upstream PRN read already scopes by `X-EPR-ORGANISATION`.

## Service Integration

Extend the existing PRN common backend service client:

```csharp
public interface IPrnCommonBackendService
{
    Task<IEnumerable<Obligation>> ReadObligations(Guid organisationId, int year, CancellationToken cancellationToken);
    Task<Prn?> ReadPrn(Guid organisationId, string prnId, CancellationToken cancellationToken);
}
```

The implementation should:

- parse `prnId` as a GUID inside the common-backend adapter while common backend is the only source, returning `null` when it is not parseable;
- call `GET api/v1/prn/{commonBackendPrnId:D}`;
- send `X-EPR-ORGANISATION` as the route organisation ID;
- preserve existing OAuth2 handler, proxy handler, header propagation, and resilience pipeline;
- return `null` on upstream `404`;
- call `EnsureSuccessStatusCode()` for other non-success statuses;
- deserialize into an upstream service model under `src/Api/Services/PrnCommonBackend`, then map to `Dtos.Prn`.

## OpenAPI Contract

The endpoint should publish:

- `200` with `Prn`;
- `401` problem;
- `403` problem;
- `404` problem;
- `500` problem.

The OpenAPI schema should model `Prn.id` and the route `prnId` parameter as plain strings with no `uuid` format. The initial common-backend adapter can still require a parseable GUID internally, but that must not leak into the public schema. `Prn.issuedAt` should be a required date-time with offset. `Prn.recipient.organisationId` should be modelled as a required string with the `uuid` format, matching the organisation route parameter. `Prn.recipient.displayName` should be required; `name`, `tradingName`, and `registrationType` should be nullable, with `registrationType` exposing only `DirectProducer` and `ComplianceScheme` when present. Every `audit` property should be nullable and documented as an ISO 8601 date-time with offset. OpenAPI property descriptions generated from DTO comments must explain each timestamp's semantics, when it is populated only by a particular source integration, and why it is `null` for other sources. The `issuedAt` description must state that it is also the RREPW/epr-backend authorisation time.

The operation can keep the existing placeholder metadata:

- operation name: `ReadOrganisationPrn`;
- tag: `PRNs`;
- summary: `PRN by ID`;
- description: `Return a PRN by organisation ID and PRN ID`.

## Testing Approach

Add or update tests in these layers:

| Layer | Tests |
| --- | --- |
| `PrnCommonBackendServiceTests` | Returns mapped PRN on `200`; returns `null` on upstream `404`; returns `null` for a non-GUID `prnId` while common backend is the only source; sends `X-EPR-ORGANISATION`; sends bearer token through existing OAuth2 handler; maps upstream `issueDate` to required `issuedAt`; maps upstream `issuedByOrg` to `issuer.organisationName`; maps upstream `organisationId` to required `recipient.organisationId`; maps upstream `organisationName` only to `recipient.displayName` and returns nullable `name`, `tradingName`, and `registrationType` as `null`; throws a data-quality exception when `obligationYear` is missing, blank, or not an integer; handles unparseable `accreditationYear` as `null`; maps `reprocessorExporterAgency`; returns unavailable accepted/rejected/cancelled audit timestamps as `null`; maps `createdOn` and `lastUpdatedDate` into nullable `DateTimeOffset` audit fields using Waste Obligations' ISO 8601 offset format; maps all known common-backend PRN statuses into the Waste Obligations PRN status vocabulary; maps all known common-backend PRN `materialName` values into the Waste Obligations PRN material vocabulary, including `Fibre`. |
| `ReadPrnTests` | Returns `200` when organisation and PRN exist; returns `404` when organisation missing; returns `404` when PRN missing; accepts a non-GUID `prnId` route value and returns `404` while common backend is the only source; returns `403` for write-only user; returns `404` on organisation mismatch as a defensive guard. |
| OpenAPI snapshot | Refresh to include the `200` schema for `Prn`, including required `issuedAt`, `recipient.organisationId` with `uuid` format, required `recipient.displayName`, nullable recipient source-detail fields, and all nullable audit date fields. Verify that nullable fields populated by only one source integration include DTO-derived OpenAPI descriptions explaining their source and null semantics, that accepted/rejected/cancelled fields describe lifecycle events, that created/updated fields describe source-store semantics, and that `issuedAt` is documented as also being the RREPW/epr-backend authorisation time. |
| Integration scenario | Stub waste organisations and PRN common backend, then verify the endpoint returns the specific PRN page baseline fields plus `issuedAt`, `issuer.organisationName`, `recipient.organisationId`, required `recipient.displayName`, nullable recipient source-detail fields, `reprocessorExporterAgency`, `accreditationYear`, and all audit dates. |
| Source compatibility | Add contract-level assertions, initially in documentation or tests, that each public Waste Obligations PRN field maps from common backend `PrnDto`, NPWD-only `legacy-prns` `LegacyPrn`, and future RREPW/epr-backend PRN data. |
| Status compatibility | Add contract-level assertions that the public PRN status possible values are `AwaitingAuthorisation`, `AwaitingAcceptance`, `Accepted`, `Rejected`, `AwaitingCancellation`, and `Cancelled`; epr-backend `draft`, `deleted`, and `discarded` should not be exposed through this endpoint. |

The `epr-packaging-frontend` `PrnMappingContractTests` are useful reference cases for status normalisation, PRN/PERN note type derivation, and null handling for `prnSignatoryPosition` and `processToBeUsed`.

## Implementation Steps

1. Add service models for upstream PRN common backend detail response.
2. Add `Dtos.Prn` with `Id` as `string`, required `IssuedAt` as `DateTimeOffset`, plus `ReprocessorExporterAgency`, `AccreditationYear`, nullable audit dates, and nested DTO records for `issuer`, `recipient`, `authorisedBy`, and `audit`. Require `recipient.organisationId` as a `Guid` and `recipient.displayName` as a string; add nullable `recipient.name` and `recipient.tradingName`. Declare `recipient.registrationType` as the existing nullable `Defra.WasteObligations.Api.Dtos.RegistrationType`; do not create a separate PRN registration-type enum. Add property-level DTO description comments to every nullable property populated by only one source integration, covering its source, semantics, and reason for being `null` elsewhere, and confirm those descriptions flow into OpenAPI. Add an `issuedAt` DTO description stating that it is also the authorisation time for RREPW/epr-backend PRNs.
3. Extend the material constants used for public API possible values with `Fibre`, then apply the PRN material possible values to `Dtos.Prn.Material`.
4. Add PRN status constants for `AwaitingAuthorisation`, `AwaitingAcceptance`, `Accepted`, `Rejected`, `AwaitingCancellation`, and `Cancelled`, then expose them as `PossibleValue` attributes on `Dtos.Prn.Status`.
5. Add mapper from upstream PRN common backend model to `Dtos.Prn`, including the common material, status, and audit-date mapping tables above. Map upstream `issueDate` to `issuedAt`, `issuedByOrg` to `issuer.organisationName`, and `organisationName` only to `recipient.displayName`; leave the richer recipient fields and unavailable event-specific audit dates null for this source.
6. Extend `IPrnCommonBackendService` and `PrnCommonBackendService` with `ReadPrn`.
7. Replace the placeholder endpoint handler with the organisation and PRN reads.
8. Add WireMock stubs and fixtures for PRN common backend detail response.
9. Add endpoint, service, OpenAPI, and integration tests.
10. Record the `legacy-prns` follow-up work for NPWD-only read API/direct integration before treating it as an NPWD legacy source for this endpoint.
11. Record the `epr-backend` follow-up work to deliver the required recipient-scoped PRN detail endpoint, including recipient-visible status rules, mixed identity support if NPWD PRNs are imported, recipient organisation snapshot fields, complete audit datetime support, and fields missing from `packagingRecyclingNoteById`, before treating this as a common schema across all sources.
12. Run CSharpier, build, `Api.Tests`, and `Api.IntegrationTests`.

## Open Questions

- Will NPWD legacy PRNs ultimately be served from `legacy-prns`, from `epr-backend`, or from both during a transition?
- Who owns delivery of the required recipient-scoped epr-backend PRN detail endpoint, and what will its final route, versioning, authentication, and recipient-visible status rules be?
- If NPWD legacy PRNs are imported into `epr-backend`, how will epr-backend support existing GUID-style legacy PRN IDs alongside ObjectId-style RREPW/future PRN IDs?
- Will epr-backend constrain `issuedToOrganisation.registrationType` to `LARGE_PRODUCER` and `COMPLIANCE_SCHEME`, or can other source values appear? Waste Obligations will map only those two known values initially and return `null` for an unrecognised value.
