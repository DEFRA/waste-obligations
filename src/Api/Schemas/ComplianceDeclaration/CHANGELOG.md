# Compliance declaration schema changelog

This changelog records changes to the versioned JSON schemas embedded in the API. Schema files are immutable; use the entry for the matching version when interpreting a persisted declaration or analytics event.

The current schema is [v1.3](compliance-declaration.v1.3.schema.json).

## Calculation correction, 2026-10-27 (MO-661)

- Corrected the implementation of the existing percentage-met metric: average each participating material's accepted/obligated percentage, capped per material at 100%, then round the exact average to a whole number away from zero. Ignore `NoDataYet`; count a zero-obligated `Met` material as 100%.
- Retained v1.3 because this is a calculation bug fix to the same intended business metric, with no change to its meaning, numeric representation or payload shape. The aggregate formula documented below describes the historical implementation used by migrations 004 and 005.
- The deployment containing MO-661 is the calculation cutover. Existing declarations whose percentage changes are updated through the application service with a version increment and a new audited event. Published schemas and historical audit snapshots remain unchanged.

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
