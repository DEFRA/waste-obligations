# Waste Obligations context

Waste Obligations supports organisations meeting packaging recycling obligations: it exposes obligation and recycling-note information, records compliance declarations, and identifies eligible organisations that have not submitted a declaration. Obligation amounts come from the PRN Common Backend. A declaration preserves what was submitted; refreshed organisation and obligation information serves a different purpose.

Use the language below across code, tests and discussions. The linked sources define implemented behaviour; the [architecture decisions](docs/adr/README.md) preserve its rationale.

## Organisations and registration

**Organisation**: The Waste Organisations business record to which this service relates obligations, declarations and PRNs. `organisationId` identifies that record, including when the record represents a compliance scheme. See the [identity contract](docs/adr/0021-waste-organisations-record-identity.md). _Avoid_: Account organisation ID or declaration ID as interchangeable names.

**Direct producer**: The registration category called `DirectProducer` in this service and `LARGE_PRODUCER` in Waste Organisations. Use the full term when the distinction from a compliance scheme matters. See the [source mapping](src/Api/Services/OrganisationEligibility/Mappers.cs).

**Compliance scheme**: A registration category with its own Waste Organisations record. **Scheme operator** means the organisation operating that scheme; its Account details can be used to resolve references and notification recipients without replacing the scheme's identity. See the [identity contract](docs/adr/0021-waste-organisations-record-identity.md) and [recipient resolution](src/Api/Services/CancellationEmailRecipientResolver.cs).

**Registration type**: The category under which an organisation is considered for an obligation year. It forms part of the identity of an unsubmitted organisation alongside organisation ID and obligation year. Registration status, declaration status and PRN status describe separate lifecycles. See the [eligibility identity contract](docs/adr/0021-waste-organisations-record-identity.md).

**Organisation reference number**: The business reference resolved for an organisation, with resolution depending on registration type. It is distinct from the Waste Organisations ID and Companies House number. See [reference resolution](src/Api/Services/OrganisationEligibility/OrganisationReferenceResolver.cs).

## Obligations and declarations

**Obligation year**: The year against which recycling obligations, declarations and eligibility are considered. “Current obligation year” follows a service policy that retains the previous year through January and changes on 1 February in Europe/London time. See the [year provider](src/Api/Services/CurrentObligationYearProvider.cs).

**Packaging year**: The submission-data year, one year before the obligation year. The [calculation processor](src/Api/ScheduledJobs/ObligationCalculationProcessor.cs) requests that year's approved full-year totals from the [Packaging API client](src/Api/Services/Packaging/PackagingSource.cs).

**Submitter**: The direct producer or compliance scheme reporting packaging data. A compliance scheme's submission contains its member producers; submitter identity and producer identity remain distinct. See the [calculator input](src/Api/Services/ObligationCalculations/ApprovedSubmission.cs) and [producer input](src/Api/Services/ObligationCalculations/ProducerPackaging.cs).

**Obligation calculation**: A locally calculated recycling requirement for a submitter and obligation year, containing producer and material results. These shadow calculations are [stored with version history](src/Api/Services/ObligationCalculations/ObligationCalculationService.cs); they do not supply current public obligation reads.

**Daily obligation calculation job**: Scheduled work that creates an instruction for the current obligation year so approved packaging totals can be recalculated and saved. Its [schedule and processing settings](src/Api/appsettings.json) default to 01:00 UTC daily, with scheduling and processing disabled. The [job](src/Api/ScheduledJobs/ObligationCalculationJob.cs) selects the obligation year and the [processor](src/Api/ScheduledJobs/ObligationCalculationProcessor.cs) requests the preceding packaging year.

**Recycling obligation**: A material-specific recycling requirement, presented with PRN information to show its associated tonnages and status. Local obligation calculations compute requirements, while current public obligation amounts and status are supplied by the PRN Common Backend. See the [integration boundary](docs/adr/0001-obligations-response-boundary.md) and [obligation mapping](src/Api/Services/PrnCommonBackend/Mappers.cs).

**Compliance declaration**: A submitted record for an organisation and obligation year, preserving selected organisation, obligation and user facts as they were submitted. Its organisation snapshot is submission evidence, rather than a live organisation profile. See the [snapshot decision](docs/adr/0003-declaration-submission-snapshots.md).

**Submitted, Accepted and Cancelled declaration**: The declaration lifecycle states. Qualify “accepted” with the subject because accepting a declaration and accepting a PRN are separate actions. “Unsubmitted” describes an eligible organisation's absence of a qualifying declaration; it is not a declaration state. See the [declaration lifecycle](src/Api/Data/Entities/ComplianceDeclaration.cs).

**Unsubmitted organisation**: An eligible organisation, obligation year and registration type without a Submitted or Accepted declaration for that same combination. A Cancelled declaration does not suppress the combination. The visible view also requires registered status and a resolved reference number. See [eligibility visibility](src/Api/Services/UnsubmittedEligibilityVisibilityService.cs).

**Obligation coverage percentage**: The service's derived coverage measure across materials with obligation data. Its meaning depends on the per-material calculation; treating it as the ratio of total accepted to total obligated tonnage changes that meaning. See the [coverage decision](docs/adr/0006-derived-obligation-coverage.md) and [calculator](src/Api/Data/Entities/ObligationCoveragePercentageCalculator.cs).

**Recycling obligations met**: The aggregate assessment used for unsubmitted organisations. An unknown result means there is no material status evidence and must remain distinct from a negative result. See the [summary mapping](src/Api/Services/OrganisationObligations/OrganisationObligationSummaryMapper.cs).

## Recycling notes

**PRN and PERN**: The two recycling-note types exposed by this service, with PERN selected for an exported note in the source. A note's source ID and its displayed note number are separate identifiers. See the [PRN integration guide](docs/prn-data-integration-guide.md) and [source mapping](src/Api/Services/PrnCommonBackend/Mappers.cs).

**PRN recipient**: The producer or compliance scheme to which the note was issued. Organisation-scoped PRN routes identify this recipient. **PRN issuer** means the reprocessor or exporter issuing the note. See the [PRN integration guide](docs/prn-data-integration-guide.md).

**PRN acceptance**: An action updating a recycling note's status; it does not accept a compliance declaration. Acceptance and rejection are the note actions exposed by the [update contract](src/Api/Dtos/UpdatePrnStatus.cs).

**December waste**: A classification supplied with a recycling note. Acceptance can include a selected obligation year; that selected year and the source's December-waste classification are distinct facts. See the [note model](src/Api/Dtos/PackagingRecyclingNote.cs) and [acceptance request](src/Api/Dtos/UpdatePrnRequest.cs).

## Analytics language

**Submission event**: In analytics event names, “submission” refers to a compliance declaration change. It does not name another business record or imply that the change was a new declaration submission. See [compliance declaration analytics events](docs/analytics-compliance-declaration-events.md).
