# Organisation PRN read endpoint design

## Purpose

Add a Waste Obligations read endpoint that returns the PRN/PERN detail needed by a frontend:

`GET /organisations/{organisationId:guid}/prns/{prnId}`

`organisationId` remains a GUID. `prnId` should be a string in the Waste Obligations route and response schema so the contract can carry common-backend GUIDs now and `epr-backend` Mongo ObjectId strings later.

The first implementation should map the existing PRN common backend detail endpoint rather than introduce a new data source:

`GET /api/v1/prn/{prnId}` with `X-EPR-ORGANISATION: {organisationId}`

The endpoint should establish a stable PRN schema owned by Waste Obligations. It should not leak the upstream common backend DTO directly, because that DTO contains persistence details and fields that are not currently rendered by the specific PRN pages.

Search, listing, bulk selection, and CSV export logic are out of scope for the initial field inventory in this document.

## Repository Sources

| Repository | Role in this design | Key local sources inspected |
| --- | --- | --- |
| `waste-obligations` | Target API that will expose the new organisation-scoped PRN endpoint. Existing endpoint style, auth policy, service client pattern, DTO pattern, WireMock tests, and OpenAPI snapshot pattern come from here. | `src/Api/Endpoints/Organisations/Prns/ReadPrn.cs`, `src/Api/Endpoints/Organisations/OrganisationEndpoints.cs`, `src/Api/Services/PrnCommonBackend/PrnCommonBackendService.cs`, `src/Api/Services/PrnCommonBackend/IPrnCommonBackendService.cs`, `src/Api/Endpoints/Organisations/Obligations/ReadObligations.cs`, `tests/Testing/Extensions/WireMock/PrnCommonBackendExtensions.cs` |
| `epr-prn-common-backend` | First upstream source for PRN detail. Its Get PRN endpoint and `PrnDto` are the initial source contract to map. | `src/EPR.PRN.Backend.API/Controllers/PrnController.cs`, `src/EPR.PRN.Backend.API/Services/PrnService.cs`, `src/EPR.PRN.Backend.API/Repositories/Repository.cs`, `src/EPR.PRN.Backend.API/Dto/PrnBaseDto.cs`, `src/EPR.PRN.Backend.API/Dto/PrnDto.cs`, `src/EPR.PRN.Backend.Data/DataModels/EPRN.cs`, `src/EPR.PRN.Backend.API.Common/Enums/EprnStatus.cs` |
| `epr-pom-api-web` | Existing gateway wrapper used by the packaging frontend. It confirms that the current Get PRN path is a passthrough over PRN common backend detail data. | `WebApiGateway/WebApiGateway.Api/Controllers/PrnController.cs`, `WebApiGateway/WebApiGateway.Api/Clients/PrnServiceClient.cs`, `WebApiGateway/WebApiGateway.Core/Models/Prns/PrnModel.cs` |
| `epr-packaging-frontend` | Current rendered frontend contract for specific PRN/PERN pages. It shows which PRN fields are displayed and which values are only derived in the UI. Search/list/CSV sources were deliberately excluded from the initial inventory. | `src/FrontendSchemeRegistration.Application/DTOs/Prns/PrnModel.cs`, `src/FrontendSchemeRegistration.UI/Controllers/Prns/PrnsController.cs`, `src/FrontendSchemeRegistration.UI/Controllers/Prns/PrnsAcceptController.cs`, `src/FrontendSchemeRegistration.UI/Controllers/Prns/PrnsRejectController.cs`, `src/FrontendSchemeRegistration.UI/Mappers/PrnModelMapper.cs`, `src/FrontendSchemeRegistration.UI/Mappers/PrnAvailableAcceptanceYearsResolver.cs`, `src/FrontendSchemeRegistration.UI/ViewModels/Prns/BasePrnViewModel.cs`, `src/FrontendSchemeRegistration.UI/ViewModels/Prns/PrnViewModel.cs`, `src/FrontendSchemeRegistration.UI/Views/Prns/SelectSinglePrn.cshtml`, `src/FrontendSchemeRegistration.UI/Views/PrnsAccept/AcceptSinglePrn.cshtml`, `src/FrontendSchemeRegistration.UI/Views/PrnsAccept/AcceptedPrn.cshtml`, `src/FrontendSchemeRegistration.UI/Views/PrnsReject/RejectSinglePrn.cshtml`, `src/FrontendSchemeRegistration.UI/Views/PrnsReject/RejectedPrn.cshtml`, `src/FrontendSchemeRegistration.UI/Views/Shared/Partials/Prns/_recyclingNoteStatus.cshtml`, `src/FrontendSchemeRegistration.UI/Views/Shared/Partials/Prns/_recyclingNoteDetails.cshtml`, `src/FrontendSchemeRegistration.UI/Resources/PrnDataResourcesLocalizer.cs` |
| `epr-prn-integration-function` | Current RREPR/RREPW sync path into `epr-prn-common-backend`. It fetches new/updated RREPR PRNs, maps them to the common backend v2 create contract, and later syncs accept/reject outcomes back to RREPR. | `src/EprPrnIntegration.Api/Functions/FetchRrepwIssuedPrnsFunction.cs`, `src/EprPrnIntegration.Common/Mappers/RrepwMappers.cs`, `src/EprPrnIntegration.Common/Models/SavePrnDetailsRequest.cs`, `src/EprPrnIntegration.Common/RESTServices/RrepwService/RrepwService.cs`, `src/EprPrnIntegration.Common/RESTServices/RrepwService/RrepwRoutes.cs`, `src/EprPrnIntegration.Common/RESTServices/PrnBackendService/PrnService.cs`, `src/EprPrnIntegration.Common/Models/Rrepw/PackagingRecyclingNote.cs` |
| `legacy-prns` | Candidate future source for legacy PRN detail if PRN common backend is decommissioned after migration. It imports PRN common backend raw data into MongoDB and preserves common-backend identity/provenance in a `Legacy` subdocument. | `src/Api/Services/PrnCommonBackend/PrnCommonBackendService.cs`, `src/Api/Services/PrnCommonBackend/PrnRawDataDto.cs`, `src/Api/Services/PrnCommonBackend/Mappers.cs`, `src/Api/Jobs/MigrateLegacyPrns.cs`, `src/Api/Data/Entities/LegacyPrn.cs`, `src/Api/Data/Entities/Legacy.cs`, `src/Api/Data/LegacyPrnRepository.cs` |
| `epr-backend` | Future PRN integration point. The new Waste Obligations PRN schema must only contain fields that can be supplied from this service, or the design must record the required `epr-backend` additions. | `src/packaging-recycling-notes/domain/model.js`, `src/packaging-recycling-notes/routes/get-by-id.js`, `src/packaging-recycling-notes/routes/list.js`, `src/packaging-recycling-notes/application/external-prn-mapper.js`, `src/packaging-recycling-notes/application/admin-prn-mapper.js`, `src/packaging-recycling-notes/repository/schema.js`, `src/packaging-recycling-notes/domain/get-process-code.js` |

## GitHub Source Links

| Repository | Source link |
| --- | --- |
| `epr-prn-common-backend` | [v1 Get PRN endpoint](https://github.com/DEFRA/epr-prn-common-backend/blob/feature/MO-354/src/EPR.PRN.Backend.API/Controllers/PrnController.cs#L39-L72), [raw-data endpoint](https://github.com/DEFRA/epr-prn-common-backend/blob/feature/MO-354/src/EPR.PRN.Backend.API/Controllers/PrnController.cs#L144-L172), [raw-data current segmentation filter](https://github.com/DEFRA/epr-prn-common-backend/blob/feature/MO-354/src/EPR.PRN.Backend.API/Repositories/Repository.cs#L400-L423), [v2 create PRN endpoint](https://github.com/DEFRA/epr-prn-common-backend/blob/feature/MO-354/src/EPR.PRN.Backend.API/Controllers/PrnControllerV2.cs#L27-L44), [v2 create request contract](https://github.com/DEFRA/epr-prn-common-backend/blob/feature/MO-354/src/EPR.PRN.Backend.API.Common/DTO/SavePrnDetailsRequestV2.cs#L5-L30), [PRN upsert identity handling](https://github.com/DEFRA/epr-prn-common-backend/blob/feature/MO-354/src/EPR.PRN.Backend.API/Repositories/Repository.cs#L478-L544) |
| `epr-packaging-frontend` | [PRN model mapper](https://github.com/DEFRA/epr-packaging-frontend/blob/main/src/FrontendSchemeRegistration.UI/Mappers/PrnModelMapper.cs#L13-L51), [selected PRN page](https://github.com/DEFRA/epr-packaging-frontend/blob/main/src/FrontendSchemeRegistration.UI/Views/Prns/SelectSinglePrn.cshtml#L29-L50), [status detail partial](https://github.com/DEFRA/epr-packaging-frontend/blob/main/src/FrontendSchemeRegistration.UI/Views/Shared/Partials/Prns/_recyclingNoteStatus.cshtml#L20-L88), [note detail partial](https://github.com/DEFRA/epr-packaging-frontend/blob/main/src/FrontendSchemeRegistration.UI/Views/Shared/Partials/Prns/_recyclingNoteDetails.cshtml#L15-L103) |
| `epr-prn-integration-function` | [RREPR PRN payload model](https://github.com/DEFRA/epr-prn-integration-function/blob/main/src/EprPrnIntegration.Common/Models/Rrepw/PackagingRecyclingNote.cs#L8-L14), [RREPR to common-backend PRN mapper](https://github.com/DEFRA/epr-prn-integration-function/blob/main/src/EprPrnIntegration.Common/Mappers/RrepwMappers.cs#L11-L36), [RREPR PRN processing loop](https://github.com/DEFRA/epr-prn-integration-function/blob/main/src/EprPrnIntegration.Api/Functions/FetchRrepwIssuedPrnsFunction.cs#L112-L168), [common-backend v2 POST client](https://github.com/DEFRA/epr-prn-integration-function/blob/main/src/EprPrnIntegration.Common/RESTServices/PrnBackendService/PrnService.cs#L32-L39), [RREPR list statuses](https://github.com/DEFRA/epr-prn-integration-function/blob/main/src/EprPrnIntegration.Common/RESTServices/RrepwService/RrepwService.cs#L37-L48) |
| `legacy-prns` | [PRN common backend raw-data client](https://github.com/DEFRA/legacy-prns/blob/feature/MO-317/src/Api/Services/PrnCommonBackend/PrnCommonBackendService.cs#L8-L40), [migration job](https://github.com/DEFRA/legacy-prns/blob/feature/MO-317/src/Api/Jobs/MigrateLegacyPrns.cs#L30-L72), [raw-data DTO](https://github.com/DEFRA/legacy-prns/blob/feature/MO-317/src/Api/Services/PrnCommonBackend/PrnRawDataDto.cs#L5-L101), [raw-data to Mongo mapper](https://github.com/DEFRA/legacy-prns/blob/feature/MO-317/src/Api/Services/PrnCommonBackend/Mappers.cs#L8-L57), [Legacy PRN Mongo document](https://github.com/DEFRA/legacy-prns/blob/feature/MO-317/src/Api/Data/Entities/LegacyPrn.cs#L5-L61), [legacy identity subdocument](https://github.com/DEFRA/legacy-prns/blob/feature/MO-317/src/Api/Data/Entities/Legacy.cs#L6-L19), [Mongo ObjectId assignment](https://github.com/DEFRA/legacy-prns/blob/feature/MO-317/src/Api/Data/LegacyPrnRepository.cs#L12-L19) |
| `epr-backend` | [current get endpoint `packagingRecyclingNoteById`](https://github.com/DEFRA/epr-backend/blob/main/src/packaging-recycling-notes/routes/get-by-id.js#L17-L46), [current get response builder](https://github.com/DEFRA/epr-backend/blob/main/src/packaging-recycling-notes/routes/get-by-id.js#L25-L42), [PRN create route storing registration/accreditation context](https://github.com/DEFRA/epr-backend/blob/main/src/packaging-recycling-notes/routes/post.js#L70-L97), [PRN create handler populating context](https://github.com/DEFRA/epr-backend/blob/main/src/packaging-recycling-notes/routes/post.js#L187-L227), [PRN domain projection](https://github.com/DEFRA/epr-backend/blob/main/src/packaging-recycling-notes/domain/model.js#L220-L250), [Mongo repository ID mapping](https://github.com/DEFRA/epr-backend/blob/main/src/packaging-recycling-notes/repository/mongodb.js#L122-L165), [accreditation-scoped list lookup](https://github.com/DEFRA/epr-backend/blob/main/src/packaging-recycling-notes/repository/mongodb.js#L173-L184), [organisation accreditation lookup](https://github.com/DEFRA/epr-backend/blob/main/src/repositories/organisations/mongodb.js#L406-L418), [external PRN mapper](https://github.com/DEFRA/epr-backend/blob/main/src/packaging-recycling-notes/application/external-prn-mapper.js#L84-L105), [admin PRN mapper](https://github.com/DEFRA/epr-backend/blob/main/src/packaging-recycling-notes/application/admin-prn-mapper.js#L11-L31) |

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
| `organisationId` | Not rendered. The route already supplies the organisation ID, but this should be checked defensively if returned upstream. |
| `organisationName` | Required. Rendered as the packaging producer or compliance scheme name. |
| `producerAgency` | Not rendered on specific PRN pages. |
| `reprocessorExporterAgency` | Not rendered on specific PRN pages. |
| `prnStatus` | Required. Drives status display, status meaning, action buttons, and accept/reject result guards. |
| `prnStatusId` | Not required. Numeric status ID. Avoid exposing as the primary status contract. |
| `tonnageValue` | Required. Rendered in details, PDF tonnage-in-words, and accept result copy. |
| `materialName` | Required. Rendered as material and used for material group copy. |
| `issuerNotes` | Required. Rendered as note with a "not provided" fallback. |
| `issuerReference` | Not rendered on specific PRN pages. |
| `prnSignatory` | Required. Rendered as authorised by. |
| `prnSignatoryPosition` | Required. Rendered as position, with null currently mapped to an empty string. |
| `signature` | Not rendered on specific PRN pages. |
| `issueDate` | Required. Rendered as date issued, issue year, and affects material localisation. |
| `processToBeUsed` | Required. Rendered as recycling process, with null currently mapped to an empty string. |
| `decemberWaste` | Required. Rendered as yes/no, drives the December warning, and participates in actionability year logic. |
| `statusUpdatedOn` | Not rendered on specific PRN pages. Present in the frontend view model but not used by the inspected specific-note views. |
| `issuedByOrg` | Required. Rendered as issued by. |
| `accreditationNumber` | Required. Rendered as accreditation number. |
| `reprocessingSite` | Required for PRNs. Rendered only when the note is a PRN, not a PERN. |
| `accreditationYear` | Not rendered on specific PRN pages. |
| `obligationYear` | Required. Rendered in the December warning and used to derive the effective acceptance year. |
| `packagingProducer` | Not rendered on specific PRN pages. |
| `createdBy` | Not rendered on specific PRN pages. |
| `createdOn` | Not rendered on specific PRN pages. |
| `lastUpdatedBy` | Not rendered on specific PRN pages. |
| `lastUpdatedDate` | Not rendered on specific PRN pages. |
| `isExport` | Required. Distinguishes PRN from PERN and controls headings, labels, copy, and PRN-only reprocessing site display. |
| `sourceSystemId` | Not rendered. Current common-backend cache segmentation/provenance only. Exclude from the Waste Obligations schema and any frontend contract. |

## PRN Identity Mapping

There is no single PRN ID shared across RREPR/RREPW, `epr-prn-common-backend`, `legacy-prns`, and `epr-backend` today. The systems currently expose separate identities:

| System or layer | Field | Type | Role |
| --- | --- | --- | --- |
| RREPR/RREPW PRN payload | `id` | `string` | Source-local PRN identifier received on first sync. Current integration stores it for common-backend cache segmentation, but it is not part of the Waste Obligations schema. |
| RREPR/RREPW PRN payload | `prnNumber` | `string` | Human/business evidence number. Used by common backend upsert matching, but not the route ID. |
| `epr-prn-integration-function` create request | `sourceSystemId` | `string?` | Current integration-only field set from RREPR/RREPW `PackagingRecyclingNote.Id` to segment RREPW-sourced records from NPWD records in common backend. |
| `epr-prn-common-backend` PRN cache | `sourceSystemId` | `string?` | Current cache segmentation/provenance field. `null` identifies NPWD-origin records in the current sync queries. Do not expose through Waste Obligations or carry into future epr-backend APIs. |
| `epr-prn-common-backend` PRN cache | `externalId` | `Guid` upstream, `string` in the Waste Obligations schema | Cache-local public PRN identity used by frontend routes and by `GET /api/v1/prn/{prnId}`. Generated when a new PRN cache row is inserted; preserved on later upserts. |
| `epr-prn-common-backend` PRN cache | `id` | `int` | Internal SQL primary key. Do not expose through Waste Obligations. |
| `legacy-prns` Mongo document | `id` / `_id` | Mongo ObjectId | Migration-local document identity assigned on insert. Compatible with a string PRN schema ID if `legacy-prns` becomes the serving source. |
| `legacy-prns` legacy subdocument | `legacy.externalId` | `Guid` upstream, `string` in the Waste Obligations schema | Preserved common-backend `externalId`. This is the continuity key for existing common-backend/frontend PRN links if `legacy-prns` replaces common backend. |
| `legacy-prns` legacy subdocument | `legacy.prnId` | `int` | Preserved common-backend SQL ID. Do not expose through Waste Obligations. |
| `legacy-prns` legacy subdocument | `legacy.sourceSystemId` | `string?` | Preserved common-backend migration metadata only. Do not expose through Waste Obligations or require from epr-backend. |
| `epr-backend` PRN store | `id` | Mongo ObjectId hex string | Mongo `_id` exposed as `id` by `packagingRecyclingNoteById`; compatible with a string PRN schema ID but not with a GUID-constrained route or DTO property. |

For a new RREPR/RREPW PRN synced into `epr-prn-common-backend`, there is no RREPR/RREPW GUID equivalent to common-backend `externalId`. The current integration carries the RREPR/RREPW payload `id` into common backend only so common backend can segment RREPW-sourced records from NPWD records. That segmentation field is not part of the proposed Waste Obligations common schema and should not appear in frontend models or future epr-backend APIs. The common-backend `externalId` is generated by the cache on first insert and is then stable for that cached PRN.

The `Prn.id` type decision is settled: it must be a string. The initial common-backend mapper should return `externalId` as a string, and a future `epr-backend` mapper can return the Mongo ObjectId hex string without a schema change. If `legacy-prns` becomes the serving source, the mapper must decide whether to expose the Mongo `_id` or the preserved `legacy.externalId`; `legacy.externalId` best preserves existing common-backend link continuity.

Future correlation with `epr-backend` should not assume that common-backend `externalId` equals `epr-backend.id`, and should not require the public epr-backend API to expose the current common-backend segmentation field. A cross-system lookup needs an explicit identity strategy, likely based on one of:

- looking up by `prnNumber` where business rules guarantee uniqueness and lifecycle semantics are understood;
- preserving common-backend `externalId`/`legacy.externalId` during migration where existing links must remain valid;
- maintaining a private migration mapping between legacy/common-backend identities and `epr-backend` Mongo ObjectIds, outside the Waste Obligations response schema.

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

The common backend v2 create request already contains all of the fields proposed for the Waste Obligations detail schema, plus additional cache/provenance fields. The RREPR mapper currently populates the specific-page baseline fields from RREPR and Waste Organisations data:

| Proposed schema input | Current RREPR sync source |
| --- | --- |
| `id` | Common backend generates `externalId`; RREPR/RREPW `PackagingRecyclingNote.Id` is currently sent only for common-backend cache segmentation and does not map to the Waste Obligations schema. |
| `number` | `PackagingRecyclingNote.PrnNumber`. |
| `type` | `PackagingRecyclingNote.IsExport`. |
| `status` | `PackagingRecyclingNote.Status.CurrentStatus`, currently mapped only for awaiting acceptance and cancelled inbound records. |
| `issueDate` | `PackagingRecyclingNote.Status.AuthorisedAt`. |
| `obligationYear` | Currently hard-coded to `"2026"` in `RrepwMappers.Map`; this needs a real source or an agreed derivation. |
| `decemberWaste` | `PackagingRecyclingNote.IsDecemberWaste`. |
| `material.name` | `PackagingRecyclingNote.Accreditation.Material`, with glass process-specific mapping. |
| `recyclingProcess` | Derived from RREPR material. |
| `tonnage` | `PackagingRecyclingNote.TonnageValue`. |
| `issuedBy.organisationName` | `PackagingRecyclingNote.IssuedByOrganisation.Name`. |
| `issuedTo.organisationName` | `IssuedToOrganisation.Name` or `TradingName`, selected using Waste Organisations registration type. |
| `authorisation.name` | `PackagingRecyclingNote.Status.AuthorisedBy.FullName`. |
| `authorisation.position` | `PackagingRecyclingNote.Status.AuthorisedBy.JobTitle`. |
| `reprocessing.site` | Formatted from `PackagingRecyclingNote.Accreditation.SiteAddress`. |
| `reprocessing.accreditationNumber` | `PackagingRecyclingNote.Accreditation.AccreditationNumber`. |
| `additionalNotes` | `PackagingRecyclingNote.IssuerNotes`. |

### RREPW Field Availability

The common schema needs `isExport`/`type`, `obligationYear`, issued-by organisation name, accreditation number, and accreditation site address because those values are rendered by the current specific PRN/PERN pages or are needed to derive values rendered by those pages.

In the RREPW payload currently consumed by `epr-prn-integration-function` through `GET v1/packaging-recycling-notes`, most of those fields are already available:

| Required common-schema field | Current RREPW payload position | Assessment |
| --- | --- | --- |
| `type` / `isExport` | `PackagingRecyclingNote.IsExport`. | Present. |
| `obligationYear` | Not present in the inspected model; `RrepwMappers.Map` currently hard-codes `"2026"`. | Missing today; due to be added. This remains a blocker until RREPW supplies it or another authoritative derivation is agreed. |
| `issuedBy.organisationName` | `PackagingRecyclingNote.IssuedByOrganisation.Name`. | Present. |
| `reprocessing.accreditationNumber` | `PackagingRecyclingNote.Accreditation.AccreditationNumber`. | Present. |
| `reprocessing.site` | `PackagingRecyclingNote.Accreditation.SiteAddress`, formatted by the integration mapper. | Present when the RREPW payload contains site address. |

Based on the inspected integration payload, RREPW is not missing any other common-schema baseline fields beyond `obligationYear`. If the future RREPW detail/read endpoint selected for Waste Obligations differs from this list/sync payload and cannot supply the baseline fields in the proposed schema, that endpoint should be treated as not fit for purpose for the common PRN contract.

In that case, request a new RREPW PRN detail endpoint that can supply, at minimum:

- PRN/PERN number;
- PRN/PERN type, either as `isExport` or an explicit note type;
- current status and status dates needed for issued/accepted/rejected/cancelled lifecycle display;
- issued date;
- obligation year;
- December waste flag;
- material and glass recycling process where relevant;
- recycling process, or enough material/accreditation data for Waste Obligations to derive it consistently;
- tonnage;
- issued-by organisation name;
- issued-to organisation name or trading name;
- authorised-by person name and position;
- accreditation number;
- reprocessing site address;
- issuer/additional notes.

## Future `legacy-prns` Integration

`legacy-prns` is a candidate future source for legacy PRN detail if `epr-prn-common-backend` is decommissioned after migration. It may also be bypassed if `epr-backend` becomes the single store for both new and legacy PRNs.

The inspected `legacy-prns` code currently:

1. Runs a Hangfire `MigrateLegacyPrns` job.
2. Deletes all existing `LegacyPrn` Mongo documents.
3. Reads paginated PRN common backend raw data from `GET api/v1/prn/raw-data`.
4. Maps each raw PRN into a `LegacyPrn` Mongo document.
5. Assigns a fresh Mongo ObjectId on insert.

The current client sends `sourceSystemId=null` to PRN common backend. In the inspected common-backend repository, that string is interpreted as `SourceSystemId == null`. This is a migration-completeness concern rather than a schema concern. Confirm the intended migration scope before common backend is decommissioned: if `legacy-prns` is meant to contain every cached PRN, including RREPR/RREPW-sourced records, the current raw-data query scope needs to be broadened.

`legacy-prns` stores two groups of PRN data:

- Display/detail data at the document root, such as `PrnNumber`, `Organisation`, `TonnageValue`, `MaterialName`, `Notes`, `PrnSignatory`, `IssueDate`, `IsDecemberWaste`, `AccreditationNumber`, `ReprocessingSite`, `ObligationYear`, `IsExport`, and `Status`.
- Common-backend provenance under `Legacy`, including SQL `PrnId`, common-backend `ExternalId`, current cache segmentation metadata, numeric `PrnStatusId`, `IssuerReference`, and `ProcessToBeUsed`.

The inspected code does not currently expose a PRN read API from `legacy-prns`; it provides the migration job and Mongo persistence. If Waste Obligations will read directly from `legacy-prns`, a read endpoint or direct service integration still needs to be designed.

### Current `legacy-prns` Position

This is the compatibility analysis against the proposed Waste Obligations schema.

| Waste Obligations field | Current `legacy-prns` position | Gap or action |
| --- | --- | --- |
| `id` | Mongo document has `_id`; `Legacy.ExternalId` preserves common-backend `externalId`. | Type-compatible because Waste Obligations models PRN IDs as strings. Choose the canonical serving identity. Use `Legacy.ExternalId` to preserve existing common-backend links, or Mongo `_id` if `legacy-prns` becomes a wholly new identity source. |
| `number` | `PrnNumber`. | No gap, rename to `number`. |
| `type` | `IsExport`. | No gap, map `false` -> `PRN`, `true` -> `PERN`. |
| `status` | `Status.CurrentStatus`, derived from common-backend numeric status ID as `accepted`, `rejected`, `cancelled`, or `awaiting-acceptance`. | Needs normalisation to the Waste Obligations status values. This source preserves `rejected`, unlike current `epr-backend` cancellation lifecycle semantics. |
| `issueDate` | `IssueDate`. | No gap. |
| `obligationYear` | `ObligationYear` as nullable integer. | No gap for valid source years. Invalid common-backend strings are already mapped to `null`. |
| `decemberWaste` | `IsDecemberWaste`. | No gap, rename to `decemberWaste`. |
| `material.name` | `MaterialName`. | No gap, but material value normalisation is still required across sources. |
| `recyclingProcess` | `Legacy.ProcessToBeUsed`. | No gap, but the field sits under the provenance subdocument rather than root detail data. |
| `tonnage` | `TonnageValue`. | No gap. |
| `issuedBy.organisationName` | `IssuedByOrg`. | No gap. |
| `issuedTo.organisationName` | `Organisation.Name`. | No gap, map from the migrated common-backend `OrganisationName`. |
| `authorisation.name` | `PrnSignatory`. | No gap. |
| `authorisation.position` | `PrnSignatoryPosition`. | No gap. |
| `reprocessing.site` | `ReprocessingSite`. | No gap. |
| `reprocessing.accreditationNumber` | `AccreditationNumber`. | No gap. |
| `additionalNotes` | `Notes`, mapped from common-backend `IssuerNotes`. | No gap, rename to `additionalNotes`. |

## Future `epr-backend` Integration

`epr-backend` is a future integration point for PRN detail. That means every field in the Waste Obligations PRN schema must either already be available from `epr-backend`, or the design must record the required `epr-backend` change.

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
| `organisationId` | `PackagingRecyclingNote.organisation.id` and organisation document `id`. | Supplied by the caller and checked against `prn.organisation.id` in the current single-PRN handler. |
| `registrationId` | `PackagingRecyclingNote.registrationId`; originally an `Organisation.registrations[].id`. | Supplied by the caller on PRN create/list routes. The PRN create route copies the route `registrationId` into the PRN document. The single-PRN get route currently includes this parameter in the path but does not read it from `params`. |
| `accreditationId` | `PackagingRecyclingNote.accreditation.id`; originally an `Organisation.accreditations[].id`, usually linked from `Organisation.registrations[].accreditationId`. | Supplied by the caller on PRN create/list/read routes. The create route snapshots the accreditation into the PRN document; the single-PRN get route fetches the current accreditation and checks the PRN snapshot accreditation ID matches the route value. |
| `prnId` | `PackagingRecyclingNote.id`. | Mongo ObjectId hex string, not a GUID and not the common-backend `externalId`. |

For `epr-backend` PRNs created through its own PRN create route, the canonical values for a later detail read should be:

- `registrationId`: the stored `PackagingRecyclingNote.registrationId`;
- `accreditationId`: the stored `PackagingRecyclingNote.accreditation.id`.

If only an organisation document is available, `registrationId` can be derived by finding the registration whose `accreditationId` matches the selected accreditation ID. That should be treated as a resolver fallback, not the source of truth for an existing PRN, because a PRN stores the registration and accreditation context it was created under.

The proposed Waste Obligations endpoint does not have these route parameters:

`GET /organisations/{organisationId:guid}/prns/{prnId}`

So Waste Obligations cannot call the current `epr-backend` `packagingRecyclingNoteById` endpoint using only the proposed route values. A future `epr-backend` integration needs one of these before it can back the Waste Obligations endpoint:

- an `epr-backend` organisation-scoped PRN detail endpoint that takes only `organisationId` and PRN identity, then resolves registration/accreditation internally;
- a lookup that returns `epr-backend` `prnId`, `registrationId`, and `accreditationId` from a canonical PRN identity that is agreed for the future contract;
- a stored migration mapping between common-backend `externalId` and `epr-backend` Mongo ObjectId plus hierarchy IDs, if existing common-backend links must remain valid.

For the common-backend-first implementation, `registrationId` and `accreditationId` are not populated because they are not needed by `epr-prn-common-backend` `GET /api/v1/prn/{prnId}`.

### Current `epr-backend` Gaps

This is the gap analysis against the proposed Waste Obligations schema, using the current `packagingRecyclingNoteById` response as the baseline.

| Waste Obligations field | Current `epr-backend` position | Gap or action |
| --- | --- | --- |
| `id` | Current PRN domain has `id` and current get returns `id`. | Type-compatible if Waste Obligations models PRN IDs as strings. Identity alignment is still required because common backend `externalId` values and `epr-backend` Mongo ObjectId values are different source-local identifiers. |
| `number` | Current get returns `prnNumber`. | No gap, rename to `number`. |
| `type` | Domain has `isExport`; external mapper exposes `isExport`; current get returns `wasteProcessingType` but not `isExport`. RREPW payload has `isExport`. | Add `isExport` or a direct PRN/PERN `type` to the current get response, or define a reliable mapping from `wasteProcessingType`. |
| `status` | Current get returns `status` from the domain `status.currentStatus`, using `epr-backend` snake_case values. | Needs normalisation to Waste Obligations status values. Also resolve the semantic mismatch between common-backend `Rejected` and `epr-backend` cancellation lifecycle statuses such as `awaiting_cancellation` and `cancelled`. |
| `issueDate` | Current get returns `issuedAt`. | No data gap for issued PRNs; rename to `issueDate`. |
| `obligationYear` | Not present in the PRN domain projection or current get response. It is also not present in the inspected RREPW payload, but is due to be added. | Missing. Add an explicit obligation year to `epr-backend`, or agree a derivation. Do not rely on the current RREPR hard-coded `"2026"`. |
| `decemberWaste` | Current get returns `isDecemberWaste`. | No gap, rename to `decemberWaste`. |
| `material.name` | Current get returns `material`; domain has accreditation material. | No data gap, but material value normalisation is required because current values are `epr-backend` material codes/names, while common backend/frontend values are display-oriented. |
| `recyclingProcess` | Current get returns `processToBeUsed`. | No gap, rename to `recyclingProcess`. |
| `tonnage` | Current get returns `tonnage`. | No gap. |
| `issuedBy.organisationName` | Domain has `organisation.name`; external mapper exposes `issuedByOrganisation`; current get does not return it. RREPW payload has `issuedByOrganisation.name`. | Missing from current get response, but not missing from the inspected RREPW payload. Add issued-by organisation details or expose them through a future detail contract. |
| `issuedTo.organisationName` | Current get returns `issuedToOrganisation`. | No gap, map `issuedToOrganisation.name`. |
| `authorisation.name` | Current get returns `issuedBy` actor with `name`. | No gap for issued PRNs, map from `issuedBy.name`. |
| `authorisation.position` | Current get returns `issuedBy` actor with optional `position`. | No gap when source carries it, map from `issuedBy.position`. |
| `reprocessing.site` | Domain accreditation has `siteAddress`; external mapper exposes `accreditation.siteAddress`; current get does not return it. RREPW payload has `accreditation.siteAddress`. | Missing from current get response, but not missing from the inspected RREPW payload. Add accreditation site address and format it for the Waste Obligations schema. |
| `reprocessing.accreditationNumber` | Domain accreditation has `accreditationNumber`; admin mapper exposes it; current get does not return it. RREPW payload has `accreditation.accreditationNumber`. | Missing from current get response, but not missing from the inspected RREPW payload. Add accreditation number to the current get response or future detail contract. |
| `additionalNotes` | Current get returns `notes`. | No gap, rename to `additionalNotes`. |

### Questions For `epr-backend` Before RREPW Integration

These questions need answering before RREPW/epr-backend can be integrated into the Waste Obligations PRN schema. They should not block the initial common-backend-backed implementation, but they do block treating the schema as source-complete across PRN services.

Decision already made: `Prn.id` is a string so the same public schema can carry common-backend GUID strings now and `epr-backend` Mongo ObjectId strings later.

1. Which endpoint should Waste Obligations call: the current accreditation-scoped `packagingRecyclingNoteById`, a new organisation-scoped detail endpoint, or the external RREPW-style PRN contract?
2. If the current route stays, how should Waste Obligations discover `registrationId` and `accreditationId` from only `organisationId` and `prnId`?
3. If PRNs are migrated from common backend or `legacy-prns`, what private migration mapping will connect existing common-backend `externalId` GUID strings to future `epr-backend` ObjectId strings?
4. Can the epr-backend detail response expose the common-schema fields that are present in the RREPW payload or epr-backend domain but missing from the current `packagingRecyclingNoteById` response: `isExport`/`type`, issued-by organisation name, accreditation number, and accreditation site address?
5. Once `obligationYear` is added to RREPW, will epr-backend persist and expose it on the PRN detail response, or is another authoritative source/derivation required?
6. How should statuses map? `epr-backend` has `awaiting_cancellation`/`cancelled`, while common backend has `Rejected`. Is producer rejection a status, an event, or a cancellation workflow?
7. Should draft, awaiting authorisation, deleted, or discarded `epr-backend` PRNs be hidden from this endpoint, returned with extra statuses, or mapped to `404`?
8. Are PRN tonnages guaranteed to be whole numbers long term? The frontend/common-backend path is integer-based, but `epr-backend` storage allows numeric values in places.
9. When legacy PRNs move, will `epr-backend` store `legacy.externalId` or another continuity key so existing links keep working, or will Waste Obligations need an ID translation layer?

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
| `IssueYear` | `issueDate`. |
| `DateIssuedDisplay` | `issueDate`. |
| `DecemberWasteDisplay` | `decemberWaste`. |
| `ApprovalStatusExplanationTranslation` | `type`, `status`. |
| `ApprovalStatusDisplayCssColour` | `status`. |
| `MaterialGroup` | `material.name`. |
| Localised material display | `material.name`, `issueDate`. |
| `AvailableAcceptanceYears`, `IsStatusEditable`, `EffectiveAcceptanceYear` | `status`, `obligationYear`, `decemberWaste`, current compliance year. |
| PDF tonnage in words | `tonnage`. |

## Proposed Waste Obligations Schema

Add a new API DTO named `Prn` under `src/Api/Dtos`.

The first response shape should contain the specific PRN page baseline only:

```json
{
  "id": "0d2f531d-0213-494b-8c8b-4133051bd44f",
  "number": "PRN123",
  "type": "PRN",
  "status": "AwaitingAcceptance",
  "issueDate": "2025-06-15T10:30:00+00:00",
  "obligationYear": 2025,
  "decemberWaste": false,
  "material": {
    "name": "Aluminium"
  },
  "recyclingProcess": "R3",
  "tonnage": 999,
  "issuedBy": {
    "organisationName": "Acme Reprocessors Ltd"
  },
  "issuedTo": {
    "organisationName": "Test Producer Ltd"
  },
  "authorisation": {
    "name": "Jane Smith",
    "position": "Director"
  },
  "reprocessing": {
    "site": "42 Factory Road, Manchester",
    "accreditationNumber": "ACC123"
  },
  "additionalNotes": "Important note about this PRN"
}
```

### Schema Notes

- `id` is a string. In the first common-backend-backed implementation it is the upstream common-backend `externalId` formatted as a string. In a future `epr-backend` implementation it can be the Mongo ObjectId hex string. It is not the common-backend SQL integer `id`, and it is not the RREPR/RREPW source `id`.
- `sourceSystemId` is excluded from the Waste Obligations schema. It is a current common-backend cache segmentation/provenance detail and should not be required by frontend consumers or future epr-backend APIs.
- `number` is the existing PRN/PERN evidence number.
- `type` is derived from `isExport`: `PRN` when `false`, `PERN` when `true`.
- `status` should be normalised to a Waste Obligations enum/string set: `Accepted`, `Rejected`, `Cancelled`, `AwaitingAcceptance`.
- `material.name` should initially pass through the upstream material name, with a later shared material normalisation decision if consumers need stable material codes.
- `tonnage` remains an integer because upstream common backend and the current frontend model both use `int`.
- `obligationYear` should be a nullable integer in the Waste Obligations schema. If the upstream string cannot be parsed, map to `null` and log at warning level rather than failing the whole PRN read.
- `recyclingProcess`, `authorisation.name`, `authorisation.position`, `reprocessing.site`, and `additionalNotes` should be nullable strings. The frontend can continue applying empty-string or "not provided" fallbacks.
- `reprocessing.site` is only rendered by the current frontend for PRNs, but it can remain present and nullable in the schema for both note types.
- Non-rendered upstream fields should not be added to the first public schema unless a consuming page or service integration needs them.
- The schema must remain source-compatible with both possible future legacy sources: `legacy-prns` and `epr-backend`. Before any new public field is added, confirm that it exists in the current `legacy-prns` Mongo document and current `epr-backend` PRN domain projection, or record the source change required to supply it.
- `legacy-prns` can supply the specific-page baseline fields from its current Mongo document shape, but a read API/direct integration and canonical identity choice still need to be designed before it can replace common backend for this endpoint.
- The fields currently missing from the `epr-backend` `packagingRecyclingNoteById` response are `type`/`isExport`, `obligationYear`, `issuedBy.organisationName`, `reprocessing.site`, and `reprocessing.accreditationNumber`. PRN identity and status semantics also need explicit alignment before `epr-backend` can replace or sit beside common backend as the detail source.

## Field Mapping

| Waste Obligations field | PRN common backend field | Transform |
| --- | --- | --- |
| `id` | `externalId` | Format the upstream GUID as a string. Do not expose it as a `Guid`/`uuid` schema field. |
| `number` | `prnNumber` | Direct. |
| `type` | `isExport` | `true` -> `PERN`, `false` -> `PRN`. |
| `status` | `prnStatus` or `prnStatusId` | Prefer `prnStatus`; normalise `AWAITINGACCEPTANCE` to `AwaitingAcceptance`, `CANCELLED`/`CANCELED` to `Cancelled`. |
| `issueDate` | `issueDate` | Convert to `DateTimeOffset` or preserve as a UTC-compatible instant. |
| `obligationYear` | `obligationYear` | Parse string to nullable int. |
| `decemberWaste` | `decemberWaste` | Direct. |
| `material.name` | `materialName` | Direct initially. |
| `recyclingProcess` | `processToBeUsed` | Direct nullable string. |
| `tonnage` | `tonnageValue` | Direct. |
| `issuedBy.organisationName` | `issuedByOrg` | Direct. |
| `issuedTo.organisationName` | `organisationName` | Direct. |
| `authorisation.name` | `prnSignatory` | Direct nullable string. |
| `authorisation.position` | `prnSignatoryPosition` | Direct nullable string. |
| `reprocessing.site` | `reprocessingSite` | Direct nullable string. |
| `reprocessing.accreditationNumber` | `accreditationNumber` | Direct. |
| `additionalNotes` | `issuerNotes` | Direct nullable string. |

## Endpoint Behaviour

The endpoint should follow the existing organisation endpoint pattern:

1. Require `PolicyNames.Read`.
2. Bind `organisationId` from the route as `Guid` and `prnId` from the route as `string`.
3. Read the organisation using `IWasteOrganisationsService.Read(organisationId, cancellationToken)`.
4. Read the PRN using `IPrnCommonBackendService.ReadPrn(organisationId, prnId, cancellationToken)`.
5. Run both reads concurrently where practical.
6. Return `404` when the organisation is not found.
7. Return `404` when PRN common backend returns `404` or `null`.
8. Return `404` if the mapped upstream `organisationId` is present and does not match the route `organisationId`.
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

The OpenAPI schema should model `Prn.id` and the route `prnId` parameter as plain strings with no `uuid` format. The initial common-backend adapter can still require a parseable GUID internally, but that must not leak into the public schema.

The operation can keep the existing placeholder metadata:

- operation name: `ReadOrganisationPrn`;
- tag: `PRNs`;
- summary: `PRN by ID`;
- description: `Return a PRN by organisation ID and PRN ID`.

## Testing Approach

Add or update tests in these layers:

| Layer | Tests |
| --- | --- |
| `PrnCommonBackendServiceTests` | Returns mapped PRN on `200`; returns `null` on upstream `404`; returns `null` for a non-GUID `prnId` while common backend is the only source; sends `X-EPR-ORGANISATION`; sends bearer token through existing OAuth2 handler; handles unparseable `obligationYear` as `null`. |
| `ReadPrnTests` | Returns `200` when organisation and PRN exist; returns `404` when organisation missing; returns `404` when PRN missing; accepts a non-GUID `prnId` route value and returns `404` while common backend is the only source; returns `403` for write-only user; returns `404` on organisation mismatch as a defensive guard. |
| OpenAPI snapshot | Refresh to include `200` schema for `Prn`. |
| Integration scenario | Stub waste organisations and PRN common backend, then verify the endpoint returns the specific PRN page baseline fields listed above. |
| Source compatibility | Add contract-level assertions, initially in documentation or tests, that each public Waste Obligations PRN field maps from common backend `PrnDto`, `legacy-prns` `LegacyPrn`, and `epr-backend` PRN data. |

The `epr-packaging-frontend` `PrnMappingContractTests` are useful reference cases for status normalisation, PRN/PERN note type derivation, and null handling for `prnSignatoryPosition` and `processToBeUsed`.

## Implementation Steps

1. Add service models for upstream PRN common backend detail response.
2. Add `Dtos.Prn` with `Id` as `string`, plus nested DTO records for `material`, `issuedBy`, `issuedTo`, `authorisation`, and `reprocessing`.
3. Add mapper from upstream PRN common backend model to `Dtos.Prn`.
4. Extend `IPrnCommonBackendService` and `PrnCommonBackendService` with `ReadPrn`.
5. Replace the placeholder endpoint handler with the organisation and PRN reads.
6. Add WireMock stubs and fixtures for PRN common backend detail response.
7. Add endpoint, service, OpenAPI, and integration tests.
8. Record the `legacy-prns` follow-up work for read API/direct integration, canonical ID selection, and migration scope before treating it as a replacement for common backend.
9. Record the `epr-backend` follow-up work for fields missing from `packagingRecyclingNoteById` before treating this as a common schema across all sources.
10. Run CSharpier, build, `Api.Tests`, and `Api.IntegrationTests`.

## Open Questions

- Should Waste Obligations own `AvailableAcceptanceYears`, `IsStatusEditable`, and `EffectiveAcceptanceYear`, or should those remain frontend-derived until accept/reject behaviour moves behind this API?
- Should `material.name` remain a display/source name only, or should the schema include a stable material code alongside it?
- Should the API return display-ready status labels, or only normalised status values and let the frontend keep localisation and status copy ownership?
- Should non-rendered fields such as `producerAgency`, `reprocessorExporterAgency`, `issuerReference`, `signature`, `accreditationYear`, `packagingProducer`, and audit dates be excluded until a consumer needs them?
- Should invalid upstream `obligationYear` be mapped to `null`, or should the endpoint fail with `500` to surface data quality issues sooner?
- Should `obligationYear` be stored explicitly in `epr-backend`, derived from accreditation/year data, or supplied by RREPR? The current RREPR sync hard-codes `"2026"`, so it should not be treated as a reliable long-term mapping.
- Will legacy PRNs ultimately be served from `legacy-prns`, from `epr-backend`, or from both during a transition?
- If `legacy-prns` is used, should its migration include only current NPWD-segmented common-backend records, or every cached PRN including RREPR/RREPW-sourced records?
- Which source-local PRN ID should be considered canonical when the same PRN can be read from common backend, `legacy-prns`, and `epr-backend`?
- What status vocabulary should the common schema use when common backend/frontend have `Rejected`, while `epr-backend` models producer rejection as a cancellation lifecycle rather than a current `rejected` status?
