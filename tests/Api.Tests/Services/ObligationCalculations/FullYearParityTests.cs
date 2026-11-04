using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using EPR.PRN.Backend.API.Common.Enums;
using EPR.PRN.Backend.Data.DataModels;
using EPR.PRN.Backend.Obligation.Dto;
using EPR.PRN.Backend.Obligation.Interfaces;
using EPR.PRN.Backend.Obligation.Models;
using EPR.PRN.Backend.Obligation.Services;
using EPR.PRN.Backend.Obligation.Strategies;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using ObligationCalculation = Defra.WasteObligations.Api.Data.Entities.ObligationCalculation;

namespace Defra.WasteObligations.Api.Tests.Services.ObligationCalculations;

public class FullYearParityTests
{
    private static readonly Guid Submitter = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Producer = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset CalculationTime = new(2026, 5, 4, 12, 30, 0, TimeSpan.Zero);

    public static TheoryData<string> CaseIds => new(Cases().Select(x => x.Id));

    [Theory]
    [MemberData(nameof(CaseIds))]
    public void FullYearCorpus_ShouldMatchPinnedLegacyArithmeticAndMappedRecords(string caseId)
    {
        var example = Cases().Single(x => x.Id == caseId);
        var legacy = new MaterialCalculationService();
        var obligations =
            example.Material == "GL"
                ? GlassObligations(legacy, example)
                : new[] { legacy.Calculate(example.Target, example.Tonnes, null, example.Year) };

        obligations.Should().Equal(example.Expected);

        foreach (
            var submitterType in new[]
            {
                ObligationSubmitterType.DirectRegistrant,
                ObligationSubmitterType.ComplianceScheme,
            }
        )
        {
            var producerId = submitterType == ObligationSubmitterType.DirectRegistrant ? Submitter : Producer;
            var calculator = Calculator(example);
            var result = calculator.Calculate(
                new ApprovedSubmission
                {
                    SubmitterOrganisationId = Submitter,
                    SubmitterType = submitterType,
                    Producers =
                    [
                        new ProducerPackaging
                        {
                            ProducerOrganisationId = producerId,
                            Materials =
                            [
                                new MaterialWeight
                                {
                                    MaterialCode = example.Material,
                                    Kilograms = (long)example.Tonnes * 1_000,
                                },
                            ],
                        },
                    ],
                },
                example.Year
            );
            var before = DateTime.UtcNow;
            var legacyRecords = LegacyRecords(example, producerId, submitterType);
            var after = DateTime.UtcNow;
            legacyRecords.Select(x => x.MaterialObligationValue).Should().Equal(example.Expected);
            legacyRecords.Should().OnlyContain(x => x.CalculatedOn >= before && x.CalculatedOn <= after);
            legacyRecords.Select(x => x.CalculatedOn).Distinct().Should().ContainSingle();
            var legacyRows = legacyRecords
                .Select(x => new MappedRow(
                    x.OrganisationId,
                    x.SubmitterId,
                    x.SubmitterTypeId == 2 ? "DirectRegistrant" : "ComplianceScheme",
                    x.Year,
                    Materials().Single(material => material.Id == x.MaterialId).MaterialCode,
                    x.Tonnage,
                    x.MaterialObligationValue,
                    CalculationTime.UtcDateTime
                ))
                .ToArray();

            Flatten(result).Should().Equal(legacyRows);
        }
    }

    private static List<EPR.PRN.Backend.Data.DataModels.ObligationCalculation> LegacyRecords(
        ParityCase example,
        Guid producerId,
        ObligationSubmitterType submitterType
    )
    {
        var material = Materials().Single(x => x.MaterialCode == example.Material);
        var materialType = Enum.Parse<MaterialType>(material.MaterialName);
        IMaterialCalculationStrategy strategy =
            example.Material == "GL"
                ? new GlassCalculationStrategy(new MaterialCalculationService())
                : new GeneralCalculationStrategy(new MaterialCalculationService());

        return strategy.Calculate(
            new CalculationRequestDto
            {
                SubmitterId = Submitter,
                SubmitterTypeId = submitterType == ObligationSubmitterType.DirectRegistrant ? 2 : 1,
                ComplianceYear = example.Year,
                MaterialType = materialType,
                Materials = Materials(),
                SubmissionCalculationRequest = new SubmissionCalculationRequest
                {
                    OrganisationId = producerId,
                    SubmitterId = Submitter,
                    SubmitterType = submitterType.ToString(),
                    SubmissionPeriod = (example.Year - 1).ToString(),
                    PackagingMaterial = example.Material,
                    PackagingMaterialWeight = example.Tonnes,
                    NumberOfDaysObligated = null,
                },
                RecyclingTargets = new Dictionary<int, Dictionary<MaterialType, double>>
                {
                    [example.Year] = new Dictionary<MaterialType, double>
                    {
                        [materialType] = example.Target,
                        [MaterialType.GlassRemelt] = example.RemeltTarget,
                    },
                },
            }
        );
    }

    private static List<Material> Materials() =>
        [
            new()
            {
                Id = 1,
                MaterialCode = "PL",
                MaterialName = "Plastic",
            },
            new()
            {
                Id = 2,
                MaterialCode = "PC",
                MaterialName = "Paper",
            },
            new()
            {
                Id = 3,
                MaterialCode = "FC",
                MaterialName = "FibreComposite",
            },
            new()
            {
                Id = 4,
                MaterialCode = "GL",
                MaterialName = "Glass",
            },
            new()
            {
                Id = 5,
                MaterialCode = "GR",
                MaterialName = "GlassRemelt",
            },
            new()
            {
                Id = 6,
                MaterialCode = "AL",
                MaterialName = "Aluminium",
            },
            new()
            {
                Id = 7,
                MaterialCode = "ST",
                MaterialName = "Steel",
            },
            new()
            {
                Id = 8,
                MaterialCode = "WD",
                MaterialName = "Wood",
            },
        ];

    [Fact]
    public void SqlIntBoundary_ShouldMapMaximumAndRejectLargerDestinationTonnage()
    {
        var example = new ParityCase("SQL-int-maximum", "PL", int.MaxValue, 2025, 1, 0, [int.MaxValue]);
        var calculator = Calculator(example);
        var submission = new ApprovedSubmission
        {
            SubmitterOrganisationId = Submitter,
            SubmitterType = ObligationSubmitterType.DirectRegistrant,
            Producers =
            [
                new ProducerPackaging
                {
                    ProducerOrganisationId = Submitter,
                    Materials = [new MaterialWeight { MaterialCode = "PL", Kilograms = (long)int.MaxValue * 1_000 }],
                },
            ],
        };
        var maximum = calculator.Calculate(submission, 2025);
        var legacy = new MaterialCalculationService().Calculate(1, int.MaxValue, null, 2025);

        Flatten(maximum).Single().InputTonnage.Should().Be(int.MaxValue);
        Flatten(maximum).Single().Obligation.Should().Be(legacy);

        var larger = calculator.Calculate(
            submission with
            {
                Producers =
                [
                    submission.Producers[0] with
                    {
                        Materials =
                        [
                            new MaterialWeight { MaterialCode = "PL", Kilograms = ((long)int.MaxValue + 1) * 1_000 },
                        ],
                    },
                ],
            },
            2025
        );
        var map = () => Flatten(larger).ToArray();

        larger.Producers.Single().Materials.Single().InputTonnage.Should().Be((long)int.MaxValue + 1);
        map.Should().Throw<OverflowException>();
    }

    private static IEnumerable<MappedRow> Flatten(ObligationCalculation calculation) =>
        calculation.Producers.SelectMany(producer =>
            producer.Materials.Select(material => new MappedRow(
                producer.ProducerOrganisationId,
                calculation.SubmitterOrganisationId,
                calculation.SubmitterType.ToString(),
                calculation.ObligationYear,
                material.MaterialCode,
                checked((int)material.InputTonnage),
                checked((int)material.Obligation),
                calculation.CalculatedOn
            ))
        );

    private static int[] GlassObligations(MaterialCalculationService calculator, ParityCase example)
    {
        var (remelt, remainder) = calculator.CalculateGlass(
            example.Target,
            example.RemeltTarget,
            example.Tonnes,
            null,
            example.Year
        );

        return [remainder, remelt];
    }

    private static ObligationCalculator Calculator(ParityCase example)
    {
        var targets = new Dictionary<string, double>
        {
            ["PL"] = .55,
            ["PC"] = .75,
            ["FC"] = .75,
            ["AL"] = .61,
            ["ST"] = .80,
            ["WD"] = .45,
            ["GL"] = .74,
            ["GR"] = .75,
        };
        targets[example.Material] = example.Target;
        if (example.Material == "GL")
        {
            targets["GR"] = example.RemeltTarget;
        }

        return new ObligationCalculator(
            Options.Create(
                new ObligationCalculationOptions
                {
                    Targets = new Dictionary<int, Dictionary<string, double>> { [example.Year] = targets },
                }
            ),
            new FakeTimeProvider(CalculationTime)
        );
    }

    private static IEnumerable<ParityCase> Cases()
    {
        // Source case identifiers and exclusions are recorded in Legacy/README.md.
        yield return new("original-general-01", "PL", 100, 2026, .61, 0, [61]);
        yield return new("original-general-02", "PL", 12160, 2026, .55, 0, [6688]);
        yield return new("original-general-03", "PL", 860, 2023, .55, 0, [473]);
        yield return new("original-general-04", "PL", 1000, 2024, .88, 0, [880]);
        yield return new("original-general-05", "PL", 10000, 2025, .55, 0, [5500]);
        yield return new("original-general-06", "PL", 4000, 2024, .75, 0, [3000]);
        yield return new("original-general-07", "PL", 1000, 2024, .33, 0, [330]);
        yield return new("original-general-08", "PL", 5000, 2023, .99, 0, [4950]);
        yield return new("original-general-09", "PL", 2000, 2026, .61, 0, [1220]);
        yield return new("original-general-10", "PL", 501, 2025, .88, 0, [441]);
        yield return new("original-general-11", "PL", 700, 2023, .77, 0, [539]);
        yield return new("original-general-12", "PL", 600, 2026, .91, 0, [546]);
        yield return new("original-general-13", "PL", 10001, 2025, .05, 0, [501]);
        yield return new("original-general-14", "PL", 1001, 2024, .45, 0, [451]);
        yield return new("original-general-15", "PL", 2000, 2024, .55, 0, [1100]);
        yield return new("original-glass-01", "GL", 25, 2026, .74, .75, [5, 14]);
        yield return new("original-glass-02", "GL", 123, 2026, .7, .7, [26, 61]);
        yield return new("original-glass-03", "GL", 1051, 2024, .74, .75, [194, 584]);
        yield return new("original-glass-04", "GL", 456, 2025, .74, .74, [88, 250]);
        yield return new("original-glass-05", "GL", 5000, 2024, .75, .75, [937, 2813]);
        yield return new("original-glass-06", "GL", 250, 2025, .6, .61, [58, 92]);
        yield return new("pr466-general-PL", "PL", 101, 2025, .55, 0, [56]);
        yield return new("pr466-general-FC", "FC", 101, 2025, .75, 0, [76]);
        yield return new("pr466-general-PC", "PC", 100, 2025, .75, 0, [75]);
        yield return new("pr466-general-AL", "AL", 100, 2025, .61, 0, [61]);
        yield return new("pr466-general-ST", "ST", 100, 2025, .80, 0, [80]);
        yield return new("pr466-general-WD", "WD", 100, 2025, .45, 0, [45]);
        yield return new("pr466-glass", "GL", 101, 2025, .74, .75, [18, 57]);
        yield return new("pr466-target-year-2025", "PL", 100, 2025, .55, 0, [55]);
        yield return new("pr466-target-year-2026", "PL", 100, 2026, .57, 0, [57]);
        yield return new("pr466-rounding-exact", "PL", 20, 2025, .55, 0, [11]);
        yield return new("pr466-rounding-ceiling", "PL", 21, 2025, .55, 0, [12]);
        yield return new("pr466-glass-unrounded", "GL", 3, 2025, .74, .75, [1, 2]);
        yield return new("pr466-sql-int-maximum-steel", "ST", int.MaxValue, 2025, .80, 0, [1717986918]);

        var materials = new[] { "PL", "PC", "FC", "AL", "ST", "WD", "GL" };
        double[][] targets =
        [
            [.55, .75, .75, .61, .80, .45, .74, .75],
            [.57, .77, .77, .62, .81, .46, .76, .76],
            [.59, .79, .79, .63, .82, .47, .78, .77],
            [.61, .81, .81, .64, .83, .48, .80, .78],
            [.63, .83, .83, .65, .84, .49, .82, .79],
            [.65, .85, .85, .67, .85, .50, .85, .80],
        ];
        int[][] expected =
        [
            [55, 75, 75, 61, 80, 45, 18, 56],
            [57, 77, 77, 62, 81, 46, 18, 58],
            [59, 79, 79, 63, 82, 47, 17, 61],
            [61, 81, 81, 64, 83, 48, 17, 63],
            [63, 83, 83, 65, 84, 49, 17, 65],
            [65, 85, 85, 67, 85, 50, 17, 68],
        ];
        for (var yearIndex = 0; yearIndex < targets.Length; yearIndex++)
        {
            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                yield return new(
                    $"pr466-seeded-{2025 + yearIndex}-{materials[materialIndex]}",
                    materials[materialIndex],
                    100,
                    2025 + yearIndex,
                    targets[yearIndex][materialIndex],
                    targets[yearIndex][7],
                    materialIndex == 6
                        ? [expected[yearIndex][6], expected[yearIndex][7]]
                        : [expected[yearIndex][materialIndex]]
                );
            }
        }
    }

    private sealed record ParityCase(
        string Id,
        string Material,
        int Tonnes,
        int Year,
        double Target,
        double RemeltTarget,
        int[] Expected
    );

    private sealed record MappedRow(
        Guid ProducerId,
        Guid SubmitterId,
        string SubmitterType,
        int Year,
        string MaterialCode,
        int InputTonnage,
        int Obligation,
        DateTime CalculatedOn
    );
}
