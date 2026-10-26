# Compliance declaration schema changelog

This changelog records changes to the versioned JSON schemas embedded in the API. Schema files are immutable; use the entry for the matching version when interpreting a persisted declaration or analytics event.

The current schema is [v2.0](compliance-declaration.v2.0.schema.json).

## v2.0

- Changes the meaning of optional numeric `obligationCoveragePercentage` from aggregate-tonnage coverage to the equally weighted average of individual material percentages. This is a breaking semantic change and therefore a major version; the field name, numeric representation and optionality are unchanged.
- Exclude `NoDataYet` rows. Cap each participating row's `accepted / obligated × 100` at 100%; a zero-accepted, zero-obligated `Met` row contributes 100%, and other included zero-obligation rows contribute zero. Average without rounding individual rows, then round the result to a whole number with midpoint rounding away from zero. Empty and all-`NoDataYet` snapshots return zero. Separate glass and Fibre rows remain independent.
- Mongo migration `021_ComplianceDeclarationIndividualMaterialCoverage` recalculates live v1.3 declarations from their embedded obligations and atomically advances each percentage and schema version. Earlier migrations first bring legacy declarations to v1.3. Status, year, obligations, audit entries, submission details and concurrency version are preserved; already-target and newer documents are untouched.
- Historical audit events, their snapshots and dispatch state remain unchanged and all prior schemas stay embedded. No synthetic business events are generated. Rollback does not restore the old percentage; `DownAsync` explicitly refuses reversal because the original value was not retained.
- Old hosts may still write old-formula declarations during rolling deployment or replace corrected data from stale reads. This accepted overlap has no special reconciliation or backfill beyond the migration; unsubmitted summaries update through normal refreshes.

## v1.3

- Added the optional `organisation.businessCountry` property. Values are stored verbatim from waste-organisations so newly introduced country codes remain persistable; the search API advertises its currently supported country values separately.
- This is a backwards-compatible minor version: v1.2 payloads remain valid under v1.3.
- Mongo migration `008_ComplianceDeclarationBusinessCountry` advances existing v1.2 declaration documents to v1.3 without attempting to infer a historical value. It preserves any country already written by a newer host. Historical audit-event snapshots remain on their recorded schema version.

## v1.2

- Added the optional root `obligationCoveragePercentage` property as a JSON number.
- This is a backwards-compatible minor version: v1.1 payloads remain valid under v1.2.
- Mongo migration `004_ComplianceDeclarationObligationCoveragePercentage` calculates and backfills the value for existing v1.1 declarations. Historical audit-event snapshots remain on v1.1.
- `obligationCoveragePercentage` is calculated as `sum(tonnages.accepted) / sum(tonnages.obligated) × 100`, capped at 100%, and rounded to the nearest whole number on submit and when recalculated by Mongo migration `005_ComplianceDeclarationObligationCoveragePercentagePrecision` for existing v1.2 declarations.

## v1.1

- Added the optional `locale` property to `audit[*].user`. It accepts `"en"`, `"cy"`, or `null`.
- This is a backwards-compatible minor version: v1.0 payloads remain valid under v1.1.
- Mongo migration `003_ComplianceDeclarationUserLocale` backfills the missing submitted-user locale with `null` for existing v1.0 declarations. Historical audit-event snapshots remain on v1.0.

## v1.0

- Initial compliance declaration schema, covering declaration, organisation, obligation, audit-entry, reason-audit-entry, address, and user payload shapes.
