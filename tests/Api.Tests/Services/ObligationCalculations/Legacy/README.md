# Full-year PRN assertion mapping

`FullYearParityTests` exercises this repository's calculator against the expected values in [PRN PR #466](https://github.com/DEFRA/epr-prn-common-backend/pull/466) and the original material calculation tests. The upstream reference is pinned to [revision 488028bff5b52658cc82af831d0dcfd833f71dcf](https://github.com/DEFRA/epr-prn-common-backend/tree/488028bff5b52658cc82af831d0dcfd833f71dcf). No PRN implementation code is included or executed in this repository.

For each named case, the test supplies the source targets and converts whole tonnes to kilograms by multiplying by 1,000. It asserts the material code, input tonnage and literal obligation expected by the upstream assertion, together with producer, submitter, type, obligation year and a controlled UTC timestamp. Original arithmetic cases have no material identity; the corpus uses PL for their general arithmetic and preserves their supplied targets and compliance years. Both submitter types exercise every case, with matching producer/submitter IDs for direct registrants and distinct IDs for schemes. The expected records are test data, not a reimplementation of the legacy arithmetic.

The implemented comparison covers 76 named full-year cases and the additional SQL integer boundary check. It allows the destination assertions to be audited against the linked upstream assertions. It does not run the legacy calculator, repository orchestration, target loader or submission-year parser, and does not compare independently sampled timestamps.

## Case mapping

The original source is [MaterialCalculationServiceTests](https://github.com/DEFRA/epr-prn-common-backend/blob/488028bff5b52658cc82af831d0dcfd833f71dcf/src/EPR.PRN.Backend.Obligation.UnitTests/Services/MaterialCalculationServiceTests.cs#L40-L91). Row numbers below count only the full-year rows, whose days argument is null; all their literal targets, tonnes, years and expected results are retained.

| Corpus ID | Upstream method/data row |
| --- | --- |
| `original-general-01` through `original-general-15` | `CasesForMaterialsExceptGlass`, full-year rows 1–15, in source order |
| `original-glass-01` through `original-glass-06` | `CasesForGlassMaterial`, full-year rows 1–6, in source order; expected tuple is mapped from remelt/remainder to GL/GR |

The PR #466 source is [CalculationParityTests](https://github.com/DEFRA/epr-prn-common-backend/blob/488028bff5b52658cc82af831d0dcfd833f71dcf/src/EPR.PRN.Backend.Obligation.UnitTests/Services/CalculationParityTests.cs#L79-L260), with its [large-weight boundary](https://github.com/DEFRA/epr-prn-common-backend/blob/488028bff5b52658cc82af831d0dcfd833f71dcf/src/EPR.PRN.Backend.Obligation.UnitTests/Services/CalculationParityTests.cs#L333-L342).

| Corpus ID | Upstream method/data row |
| --- | --- |
| `pr466-general-PL`, `FC`, `PC`, `AL`, `ST`, `WD` | `General_examples`, the six rows with null days |
| `pr466-glass` | `Glass_examples`, the row with null days |
| `pr466-target-year-2025`, `2026` | `Same_target_year_results_for_both_submitter_types`, both full-year rows |
| `pr466-rounding-exact`, `ceiling` | `General_rounding_at_and_above_an_integer_for_both_submitter_types`, 20/21 tonnes |
| `pr466-glass-unrounded` | `Glass_remelt_uses_unrounded_glass_target_for_both_submitter_types`, 3 tonnes |
| `pr466-seeded-{year}-{material}` | `Seeded_targets_produce_expected_records_for_each_year`, all six years and seven input materials; glass produces two outputs |
| `pr466-sql-int-maximum-steel` | `Large_weight_and_overflow_are_observable_at_the_calculation_boundary`, valid ST/int.MaxValue/.80 row |

The mixed `Compliance_scheme_calculates_each_member_and_preserves_identity` scenario includes a partial-year PL member and full-year PC/GL member. Its full-year PC/GL arithmetic is represented by `pr466-general-PC` and `pr466-glass`; the existing destination scheme test separately checks grouping and identities. It is not claimed that the partial-year member result was ported.

## Compatibility boundary and exclusions

The upstream [SQL model](https://github.com/DEFRA/epr-prn-common-backend/blob/488028bff5b52658cc82af831d0dcfd833f71dcf/src/EPR.PRN.Backend.Data/DataModels/ObligationCalculation.cs#L18-L28) stores both tonnage and obligation as signed 32-bit integers. The corpus covers the original ST/int.MaxValue case and an added unit-target/int.MaxValue case. The test's checked SQL projection accepts the maximum and rejects int.MaxValue + 1 tonnes. The destination deliberately retains 64-bit tonnages and obligations; a future SQL consumer must range-check both fields before writing. Wider destination results are not evidence of SQL compatibility, and this test projection is not a production SQL consumer.

The legacy synthetic target 2.0 overflow case is excluded because startup configuration deliberately accepts targets only from zero through one. Partial-year cases, upstream selection/adjustment, transitional packaging, persistence and SQL history are excluded. Missing/invalid targets, invalid identities and material codes are covered by destination validation tests as stricter behavior, not legacy parity. Separate destination kilogram-rounding tests remain because legacy inputs already contain whole tonnes.

The corpus provides baseline parity evidence for the listed assertions, including non-seeded target combinations. These finite examples do not prove equivalence for every possible input.
