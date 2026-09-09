# ADR-0014: Store obligation coverage as a derived value

**Status:** Accepted (retrospective)
**Confidence:** High
**Source PRs:** [#141](https://github.com/DEFRA/waste-obligations/pull/141), [#150](https://github.com/DEFRA/waste-obligations/pull/150), [#152](https://github.com/DEFRA/waste-obligations/pull/152)

## Decision

Calculate and persist `obligationCoveragePercentage` when a declaration is submitted, rather than calculating it only when read. Existing declarations are backfilled using the same current definition.

## Current definition

The value is `sum(accepted) / sum(obligated) × 100` across the declaration's obligations. A zero obligated total produces `0`; the result is capped at `100` and rounded to zero decimal places using `MidpointRounding.AwayFromZero`. The value is nullable in the public/persisted schema so documents from the prior schema remain representable during migration.

The calculation runs from the declaration's stored obligation snapshot at submission. Migration `005` recalculates `v1.2` documents from their raw BSON obligation values, so persisted values use the same whole-number precision.

## Consequences

- A historical declaration retains the coverage result appropriate to its submitted obligation data.
- Any future formula or rounding change is a persisted and analytics contract decision, not merely a display change.
- Search/sort can consume the stored value without recalculating every result.

## Evidence

PR #141 introduced the persisted additive `v1.2` field and its backfill. PR #150 revised precision, and PR #152 explicitly aligned the formula, cap and whole-number rounding with AMCR-207. The calculator, migration, retained schema and schema changelog are mutually consistent. `AGENTS.md`'s persisted-schema workflow corroborates the version/migration treatment.
