using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Defra.WasteObligations.Api.Tests.Services.ObligationCalculations;

public class ObligationCalculatorTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    private static readonly Guid SubmitterId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Theory]
    [InlineData(2025, 55, 75, 75, 61, 80, 45, 18, 56)]
    [InlineData(2026, 57, 77, 77, 62, 81, 46, 18, 58)]
    [InlineData(2027, 59, 79, 79, 63, 82, 47, 17, 61)]
    [InlineData(2028, 61, 81, 81, 64, 83, 48, 17, 63)]
    [InlineData(2029, 63, 83, 83, 65, 84, 49, 17, 65)]
    [InlineData(2030, 65, 85, 85, 67, 85, 50, 17, 68)]
    public void FullYear_ShouldMatchLegacyMaterialAndYearExamples(int year, params int[] expected)
    {
        var submission = Submission(["PL", "PC", "FC", "AL", "ST", "WD", "GL"], 100_000);

        var result = Calculator().Calculate(submission, year);

        result
            .Producers.Single()
            .Materials.Select(x => x.MaterialCode)
            .Should()
            .Equal("PL", "PC", "FC", "AL", "ST", "WD", "GL", "GR");
        result.Producers.Single().Materials.Select(x => x.Obligation).Should().Equal(expected.Select(x => (long)x));
        result.Producers.Single().Materials.Should().OnlyContain(x => x.InputTonnage == 100);
    }

    [Theory]
    [InlineData("PL", 101_000, 56)]
    [InlineData("FC", 101_000, 76)]
    [InlineData("PL", 20_000, 11)]
    [InlineData("PL", 21_000, 12)]
    [InlineData("PL", 12_160_000, 6688)]
    [InlineData("PL", 860_000, 473)]
    [InlineData("WD", 1_001_000, 451)]
    public void FullYear_ShouldMatchLegacyCeilingExamples(string material, long kilograms, long expected)
    {
        var result = Calculator().Calculate(Submission([material], kilograms), 2025);

        result.Producers.Single().Materials.Single().Obligation.Should().Be(expected);
    }

    [Theory]
    [InlineData(3_000, 1, 2)]
    [InlineData(25_000, 5, 14)]
    [InlineData(101_000, 18, 57)]
    [InlineData(1_051_000, 194, 584)]
    public void Glass_ShouldMatchLegacyRoundingOrder(long kilograms, long remainder, long remelt)
    {
        var result = Calculator().Calculate(Submission(["GL"], kilograms), 2025);

        result.Producers.Single().Materials.Select(x => x.Obligation).Should().Equal(remainder, remelt);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1499, 1, 1)]
    [InlineData(1500, 2, 2)]
    [InlineData(1501, 2, 2)]
    [InlineData(long.MaxValue, 9223372036854776, 5072854620270127)]
    public void Kilograms_ShouldRoundBeforeApplyingTarget(long kilograms, long tonnes, long obligation)
    {
        var result = Calculator().Calculate(Submission(["PL"], kilograms), 2025);

        result.Producers.Single().Materials.Single().InputTonnage.Should().Be(tonnes);
        result.Producers.Single().Materials.Single().Obligation.Should().Be(obligation);
    }

    [Fact]
    public void Scheme_ShouldPreserveHierarchyAndOneUtcTimestampIgnoringJoinerLeaverEvents()
    {
        var timestamp = new DateTimeOffset(2026, 5, 4, 12, 30, 0, TimeSpan.Zero).AddTicks(12345);
        var otherProducerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var submission = Submission(["GL"], 3_000) with
        {
            SubmitterType = ObligationSubmitterType.ComplianceScheme,
            Producers =
            [
                new()
                {
                    ProducerOrganisationId = otherProducerId,
                    Materials = [new() { MaterialCode = "GL", Kilograms = 3_000 }],
                    Events =
                    [
                        new()
                        {
                            Code = 1,
                            Action = EventAction.Joined,
                            Date = new DateOnly(2025, 7, 1),
                        },
                    ],
                },
                new()
                {
                    ProducerOrganisationId = SubmitterId,
                    Materials = [new() { MaterialCode = "PL", Kilograms = 101_000 }],
                },
            ],
        };

        var result = Calculator(new FakeTimeProvider(timestamp)).Calculate(submission, 2025);

        result.SubmitterOrganisationId.Should().Be(SubmitterId);
        result.SubmitterType.Should().Be(ObligationSubmitterType.ComplianceScheme);
        result.ObligationYear.Should().Be(2025);
        result.CalculatedOn.Should().Be(timestamp.UtcDateTime.AddTicks(-2345));
        result.CalculatedOn.Kind.Should().Be(DateTimeKind.Utc);
        result.Producers.Select(x => x.ProducerOrganisationId).Should().Equal(otherProducerId, SubmitterId);
        result
            .Producers[0]
            .Materials.Should()
            .BeEquivalentTo(
                new MaterialObligationCalculation[]
                {
                    new()
                    {
                        MaterialCode = "GL",
                        InputTonnage = 3,
                        Obligation = 1,
                    },
                    new()
                    {
                        MaterialCode = "GR",
                        InputTonnage = 3,
                        Obligation = 2,
                    },
                }
            );
        result.Producers[1].Materials.Single().Obligation.Should().Be(56);
    }

    [Fact]
    public void DirectRegistrant_ShouldPreserveIdentityAndYear()
    {
        var result = Calculator().Calculate(Submission(["PL"], 100_000), 2026);

        result.SubmitterOrganisationId.Should().Be(SubmitterId);
        result.SubmitterType.Should().Be(ObligationSubmitterType.DirectRegistrant);
        result.Producers.Single().ProducerOrganisationId.Should().Be(SubmitterId);
        result.ObligationYear.Should().Be(2026);
    }

    [Fact]
    public void UnconfiguredYear_ShouldRejectCalculation()
    {
        var act = () => Calculator().Calculate(Submission(["PL"], 100_000), 2031);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NullSubmission_ShouldRejectCalculation()
    {
        var act = () => Calculator().Calculate(null!, 2025);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    public void InvalidSubmission_ShouldRejectWholeCalculation(int invalidCase)
    {
        var valid = Submission(["PL"], 1_000);
        var producer = valid.Producers.Single();
        var submission = invalidCase switch
        {
            0 => valid with { SubmitterOrganisationId = Guid.Empty },
            1 => valid with { SubmitterType = (ObligationSubmitterType)9 },
            2 => valid with { Producers = [] },
            3 => valid with { Producers = null! },
            4 => valid with { Producers = [null!] },
            5 => valid with { Producers = [producer with { ProducerOrganisationId = Guid.Empty }] },
            7 => valid with { Producers = [producer, producer] },
            8 => valid with { Producers = [producer with { Materials = [] }] },
            9 => valid with { Producers = [producer with { Materials = null! }] },
            10 => valid with { Producers = [producer with { Materials = [null!] }] },
            11 => Submission(["PL", "PL"], 1_000),
            12 => Submission(["PL", "GR"], 1_000),
            13 => Submission(["PL", "XX"], 1_000),
            14 => Submission(["pl"], 1_000),
            15 => Submission([null!], 1_000),
            16 => Submission(["PL"], -1),
            _ => throw new ArgumentOutOfRangeException(nameof(invalidCase)),
        };

        var act = () => Calculator().Calculate(submission, 2025);

        act.Should().Throw<ArgumentException>();
    }

    private ObligationCalculator Calculator(TimeProvider? timeProvider = null) =>
        new(
            factory.Services.GetRequiredService<IOptions<ObligationCalculationOptions>>(),
            timeProvider ?? new FakeTimeProvider()
        );

    private static ApprovedSubmission Submission(string[] materials, long kilograms) =>
        new()
        {
            SubmitterOrganisationId = SubmitterId,
            SubmitterType = ObligationSubmitterType.DirectRegistrant,
            Producers =
            [
                new()
                {
                    ProducerOrganisationId = SubmitterId,
                    Materials = materials
                        .Select(x => new MaterialWeight { MaterialCode = x, Kilograms = kilograms })
                        .ToArray(),
                },
            ],
        };
}
