# Full-year PRN comparison corpus

`FullYearParityTests` compiles and executes the PRN material calculator and both material strategies from [DEFRA/epr-prn-common-backend at 488028bff5b52658cc82af831d0dcfd833f71dcf](https://github.com/DEFRA/epr-prn-common-backend/tree/488028bff5b52658cc82af831d0dcfd833f71dcf). The C# files in this directory are test-only source snapshots, with repository formatting applied and calculation/record-creation logic retained. They are not production dependencies. Update the pinned revision and review the case mapping when refreshing them.

The upstream source paths are:

- `src/EPR.PRN.Backend.Obligation/Services/MaterialCalculationService.cs`
- `src/EPR.PRN.Backend.Obligation/Strategies/GeneralCalculationStrategy.cs` and `GlassCalculationStrategy.cs`
- `src/EPR.PRN.Backend.Obligation/Interfaces/IMaterialCalculationService.cs` and `IMaterialCalculationStrategy.cs`
- `src/EPR.PRN.Backend.Obligation/Dto/CalculationRequestDto.cs`
- `src/EPR.PRN.Backend.Obligation/Models/SubmissionCalculationRequest.cs`
- `src/EPR.PRN.Backend.API.Common/Enums/MaterialType.cs`
- `src/EPR.PRN.Backend.Data/DataModels/Material.cs`, `PrnMaterialMapping.cs`, `ObligationCalculation.cs` and `ObligationCalculationOrganisationSubmitterType.cs`

The legacy strategies create the SQL-shaped records. The test maps legacy material lookup IDs to codes and submitter lookup IDs 1/2 to ComplianceScheme/DirectRegistrant, then compares producer, submitter, type, obligation year, material code, tonnes and obligation with the flattened destination result. Whole tonnes become kilograms by multiplication by 1,000. Original material arithmetic cases have no material identity; the corpus uses PL for their general arithmetic and preserves their supplied targets and compliance years. Both submitter types exercise every case, with matching producer/submitter IDs for direct registrants and distinct IDs for schemes.

Both implementations' timestamp contracts are checked. Legacy strategies read the wall clock and must produce UTC-time-window records with one timestamp per material calculation. The destination uses a controlled clock. Only the mapped timestamp is normalized to that controlled instant for record comparison; equality of independently sampled clock values is not claimed. This does not execute the legacy repository orchestration, target loader or submission-year parser; the corpus supplies the equivalent resolved obligation year and targets directly to the real strategies.

## Case mapping

The original source is `src/EPR.PRN.Backend.Obligation.UnitTests/Services/MaterialCalculationServiceTests.cs` at the pinned revision. Row numbers below count only the full-year rows, whose days argument is null; all their literal targets, tonnes, years and expected results are retained.

| Corpus ID | Upstream method/data row |
| --- | --- |
| `original-general-01` through `original-general-15` | `CasesForMaterialsExceptGlass`, full-year rows 1–15, in source order |
| `original-glass-01` through `original-glass-06` | `CasesForGlassMaterial`, full-year rows 1–6, in source order; expected tuple is mapped from remelt/remainder to GL/GR |

The PR #466 source is `src/EPR.PRN.Backend.Obligation.UnitTests/Services/CalculationParityTests.cs` at the same pinned revision.

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

The SQL model stores both tonnage and obligation as signed 32-bit integers. The corpus covers the original ST/int.MaxValue case and an added unit-target/int.MaxValue case. The test's checked SQL projection accepts the maximum and rejects int.MaxValue + 1 tonnes. The destination deliberately retains 64-bit tonnages and obligations; a future SQL consumer must range-check both fields before writing. Wider destination results are not evidence of SQL compatibility, and this test projection is not a production SQL consumer.

The legacy synthetic target 2.0 overflow case is excluded because startup configuration deliberately accepts targets only from zero through one. Partial-year cases, upstream selection/adjustment, transitional packaging, persistence and SQL history are excluded. Missing/invalid targets, invalid identities and material codes are covered by destination validation tests as stricter behavior, not legacy parity. Separate destination kilogram-rounding tests remain because legacy inputs already contain whole tonnes.

The corpus provides finite evidence for the listed cases, including non-seeded target combinations. It does not prove equivalence for every possible input.
