# Compliance declaration schema changelog

This changelog records changes to the versioned JSON schemas embedded in the API. Schema files are immutable; use the entry for the matching version when interpreting a persisted declaration or analytics event.

The current schema is [v1.2](compliance-declaration.v1.2.schema.json).

## v1.2

- Added the optional root `obligationCoveragePercentage` property as a JSON number.
- This is a backwards-compatible minor version: v1.1 payloads remain valid under v1.2.
- Mongo migration `004_ComplianceDeclarationObligationCoveragePercentage` calculates and backfills the value for existing v1.1 declarations. Historical audit-event snapshots remain on v1.1.
- `obligationCoveragePercentage` is rounded to the nearest whole number on submit and when recalculated by Mongo migration `005_ComplianceDeclarationObligationCoveragePercentagePrecision` for existing v1.2 declarations.

## v1.1

- Added the optional `locale` property to `audit[*].user`. It accepts `"en"`, `"cy"`, or `null`.
- This is a backwards-compatible minor version: v1.0 payloads remain valid under v1.1.
- Mongo migration `003_ComplianceDeclarationUserLocale` backfills the missing submitted-user locale with `null` for existing v1.0 declarations. Historical audit-event snapshots remain on v1.0.

## v1.0

- Initial compliance declaration schema, covering declaration, organisation, obligation, audit-entry, reason-audit-entry, address, and user payload shapes.
