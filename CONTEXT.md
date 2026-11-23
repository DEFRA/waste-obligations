# Waste Obligations context

Waste Obligations supports organisations meeting packaging recycling obligations: it exposes obligation and recycling-note information, records compliance declarations, and identifies eligible organisations that have not submitted a declaration. Obligation amounts come from the PRN Common Backend. A declaration preserves what was submitted; refreshed organisation and obligation information serves a different purpose.

Use the language below across code, tests and discussions. The linked sources define implemented behaviour; the [architecture decisions](docs/adr/README.md) preserve its rationale.

## Organisations and registration

**Organisation**: The business record to which this service relates obligations, declarations and PRNs. `organisationId` identifies that record, including when it represents a compliance scheme. See the [identity contract](docs/adr/0021-waste-organisations-record-identity.md). _Avoid_: Account organisation ID or declaration ID as interchangeable names.

**Direct producer**: A large producer registered directly rather than through a compliance scheme. The registration type is `DirectProducer`; the calculation submitter type is `DirectRegistrant`. See the [registration mapping](src/Api/Services/OrganisationEligibility/Mappers.cs) and [submitter types](src/Api/Data/Entities/ObligationSubmitterType.cs).

**Compliance scheme**: An organisation registered to report for its members. A **scheme operator** is the organisation operating the scheme; the two have distinct identities. See the [identity contract](docs/adr/0021-waste-organisations-record-identity.md) and [recipient resolution](src/Api/Services/CancellationEmailRecipientResolver.cs).

**Registration type**: The category under which an organisation is considered for an obligation year. It forms part of the identity of an unsubmitted organisation alongside organisation ID and obligation year. Registration status, declaration status and PRN status describe separate lifecycles. See the [eligibility identity contract](docs/adr/0021-waste-organisations-record-identity.md).

**Organisation reference number**: The business reference resolved for an organisation, with resolution depending on registration type. It is distinct from the Waste Organisations ID and Companies House number. See [reference resolution](src/Api/Services/OrganisationEligibility/OrganisationReferenceResolver.cs).

## Obligations and declarations

**Obligation year**: The year against which recycling obligations, declarations and eligibility are considered. “Current obligation year” follows a service policy that retains the previous year through January and changes on 1 February in Europe/London time. See the [year provider](src/Api/Services/CurrentObligationYearProvider.cs).

**Packaging year**: The year to which packaging data relates, one year before the obligation year. The calculation uses that year's approved full-year totals.[^packaging-totals]

**Packaging submitter**: The direct producer or compliance scheme reporting packaging data. A submitter can report for more than one producer; the same organisation can be both submitter and producer. A large producer may be reported by a scheme. See the [calculator input](src/Api/Services/WastePackaging/ApprovedSubmission.cs).

**Producer in a packaging calculation**: An organisation reported by a submitter, such as a holding organisation, scheme member or subsidiary. Each producer has its own material results; the calculation does not record relationships between producers. See the [producer input](src/Api/Services/WastePackaging/ProducerPackaging.cs) and [calculation result](src/Api/Data/Entities/ProducerObligationCalculation.cs).

**Obligation calculation**: A locally calculated recycling requirement for a submitter and obligation year, containing producer and material results. These shadow calculations are [stored with version history](src/Api/Services/ObligationCalculations/ObligationCalculationService.cs); they do not supply current public obligation reads.

**Daily obligation calculation job**: Scheduled work that requests recalculation for the current obligation year. See the [job](src/Api/ScheduledJobs/ObligationCalculationJob.cs) and its [settings](src/Api/appsettings.json).

**Recycling obligation**: A material-specific recycling requirement, presented with recycling-note tonnages and a status. See the [integration boundary](docs/adr/0001-obligations-response-boundary.md).

**Material obligation status**: `Met` means the obligation is met; `NotMet` means it is not met; `NoDataYet` means there is no obligation data for that material. These are distinct from declaration and recycling-note statuses. See [obligation statuses](src/Api/Dtos/ObligationStatus.cs).

**Compliance declaration**: A record for an organisation and obligation year that preserves selected organisation, obligation and user facts at submission. Its status and audit can change afterwards. See the [snapshot decision](docs/adr/0003-declaration-submission-snapshots.md).

**Declaration submitter**: The person named as submitting a compliance declaration. This is distinct from a packaging submitter. See the [declaration](src/Api/Data/Entities/ComplianceDeclaration.cs).

**Submitted, Accepted and Cancelled declaration**: The declaration lifecycle states. “Pending” is a presentation label for `Submitted`, not a separate state. Qualify “accepted” because accepting a declaration and accepting a PRN are separate actions. “Unsubmitted” is not a declaration state. See the [declaration lifecycle](src/Api/Data/Entities/ComplianceDeclaration.cs) and [view mapping](docs/unsubmitted-compliance-declarations-design.md).

**Unsubmitted organisation**: An eligible organisation with no Submitted or Accepted declaration for its obligation year and registration type; a Cancelled declaration does not suppress it. The unsubmitted view additionally requires registered status and a resolved reference number. See [eligibility visibility](src/Api/Services/UnsubmittedEligibilityVisibilityService.cs).

**Obligation coverage percentage**: The rounded, equally weighted average of coverage for materials with status data, capped at 99 when any material is `NotMet`. It is not a ratio of overall tonnages. See the [coverage decision](docs/adr/0006-derived-obligation-coverage.md) and [calculator](src/Api/Data/Entities/ObligationCoveragePercentageCalculator.cs).

**Recycling obligations met**: The aggregate assessment used for unsubmitted organisations: false if any material is `NotMet`, otherwise true if any is `Met`, otherwise unknown. See the [summary mapping](src/Api/Services/OrganisationObligations/OrganisationObligationSummaryMapper.cs).

## Recycling notes

**PRN and PERN**: The two recycling-note types exposed by this service, with PERN selected for an exported note in the source. A note's source ID and its displayed note number are separate identifiers. See the [PRN integration guide](docs/prn-data-integration-guide.md) and [source mapping](src/Api/Services/PrnCommonBackend/Mappers.cs).

**PRN recipient**: The producer or compliance scheme to which the note was issued. Organisation-scoped PRN routes identify this recipient. **PRN issuer** means the reprocessor or exporter issuing the note. See the [PRN integration guide](docs/prn-data-integration-guide.md).

**PRN acceptance**: An action updating a recycling note's status; it does not accept a compliance declaration. Acceptance and rejection are the note actions exposed by the [update contract](src/Api/Dtos/UpdatePrnStatus.cs).

**December waste**: A classification supplied with a recycling note. Acceptance can include a selected obligation year; that selected year and the source's December-waste classification are distinct facts. See the [note model](src/Api/Dtos/PackagingRecyclingNote.cs) and [acceptance request](src/Api/Dtos/UpdatePrnRequest.cs).

## Analytics language

**Submission event**: In analytics event names, “submission” refers to a compliance declaration change. It does not name another business record or imply that the change was a new declaration submission. See [compliance declaration analytics events](docs/analytics-compliance-declaration-events.md).

**Obligation calculation event**: An analytics event published when an obligation calculation is first saved or materially changes. Equivalent recalculations are not published. See [obligation calculation analytics events](docs/analytics-obligation-calculation-events.md).

[^packaging-totals]: The Packaging API is responsible for selecting eligible large producers and combining accepted H1 and H2 submissions for the same submitter. This service receives aggregated totals and cannot verify those selections or periods from the response. The [upstream contract](https://eaflood.atlassian.net/wiki/spaces/CEDGH/pages/6612879280/Packaging+endpoints) is draft; live behaviour remains unverified. See the [client](src/Api/Services/WastePackaging/WastePackagingService.cs).
