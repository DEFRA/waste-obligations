using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Obligation = Defra.WasteObligations.Api.Data.Entities.Obligation;

namespace Defra.WasteObligations.Api.Tests.Data.Entities;

public class ObligationCoveragePercentageCalculatorTests
{
    [Fact]
    public void CalculateFromObligations_WhenAcceptedExceedsObligatedOnOneMaterial_ShouldCapEachMaterial()
    {
        var obligations = new Obligation[]
        {
            ObligationFixture
                .Default()
                .With(x => x.Status, ObligationStatus.NotMet)
                .With(
                    x => x.Tonnages,
                    ObligationTonnagesFixture.Default().With(t => t.Accepted, 100).With(t => t.Obligated, 50).Create()
                )
                .Create(),
            ObligationFixture
                .Default()
                .With(x => x.Status, ObligationStatus.NotMet)
                .With(x => x.Material, Material.Glass)
                .With(
                    x => x.Tonnages,
                    ObligationTonnagesFixture.Default().With(t => t.Accepted, 0).With(t => t.Obligated, 50).Create()
                )
                .Create(),
        };

        ObligationCoveragePercentageCalculator.CalculateFromObligations(obligations).Should().Be(50m);
    }

    [Fact]
    public void CalculateFromObligations_WhenObligationsAreNotAnArray_ShouldCalculateFromEnumerable()
    {
        var obligations = new List<Obligation>
        {
            ObligationFixture
                .Default()
                .With(x => x.Status, ObligationStatus.NotMet)
                .With(
                    x => x.Tonnages,
                    ObligationTonnagesFixture.Default().With(t => t.Accepted, 2).With(t => t.Obligated, 5).Create()
                )
                .Create(),
        };

        ObligationCoveragePercentageCalculator.CalculateFromObligations(obligations).Should().Be(40m);
    }

    [Theory]
    [InlineData(90, 90, 0, 10, 50)]
    [InlineData(1, 6, 1, 3, 25)]
    [InlineData(107, 200, 107, 200, 54)]
    public void CalculateFromObligations_WhenMaterialsHaveDifferentWeights_ShouldAverageBeforeRounding(
        int firstAccepted,
        int firstObligated,
        int secondAccepted,
        int secondObligated,
        decimal expected
    )
    {
        Obligation[] obligations =
        [
            CreateObligation(firstAccepted, firstObligated),
            CreateObligation(secondAccepted, secondObligated),
        ];

        ObligationCoveragePercentageCalculator.CalculateFromObligations(obligations).Should().Be(expected);
    }

    [Theory]
    [InlineData(4, 25)]
    [InlineData(5, 20)]
    [InlineData(6, 17)]
    public void CalculateFromObligations_WhenNoDataYetIsPresent_ShouldCountOnlyParticipatingRows(
        int count,
        decimal expected
    )
    {
        var obligations = Enumerable
            .Range(0, count)
            .Select(x => CreateObligation(x == 0 ? 10 : 0, 10))
            .Append(CreateObligation(0, 100, ObligationStatus.NoDataYet));

        ObligationCoveragePercentageCalculator.CalculateFromObligations(obligations).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, ObligationStatus.Met, 50)]
    [InlineData(0, ObligationStatus.NotMet, 0)]
    [InlineData(1, ObligationStatus.Met, 0)]
    [InlineData(0, ObligationStatus.NoDataYet, 0)]
    public void CalculateFromObligations_WhenObligationIsZero_ShouldApplyOnlyTheLowTonnageMetException(
        int accepted,
        string status,
        decimal expected
    )
    {
        Obligation[] obligations = [CreateObligation(accepted, 0, status), CreateObligation(0, 10)];

        ObligationCoveragePercentageCalculator.CalculateFromObligations(obligations).Should().Be(expected);
    }

    [Fact]
    public void CalculateFromObligations_WhenNoRowsParticipate_ShouldReturnZero()
    {
        ObligationCoveragePercentageCalculator.CalculateFromObligations([]).Should().Be(0);
        ObligationCoveragePercentageCalculator
            .CalculateFromObligations([CreateObligation(0, 0, ObligationStatus.NoDataYet)])
            .Should()
            .Be(0);
    }

    [Theory]
    [InlineData(false, 67)]
    [InlineData(true, 50)]
    public void CalculateFromObligations_WhenGlassPaperAndFibreAreSeparate_ShouldKeepEveryRowIndependent(
        bool includeFibre,
        decimal expected
    )
    {
        List<Obligation> obligations =
        [
            CreateObligation(90, 90) with
            {
                Material = "GlassRemelt",
            },
            CreateObligation(0, 10) with
            {
                Material = "GlassOther",
            },
            CreateObligation(10, 10) with
            {
                Material = "Paper",
            },
        ];
        if (includeFibre)
            obligations.Add(CreateObligation(0, 10) with { Material = "Fibre" });

        ObligationCoveragePercentageCalculator.CalculateFromObligations(obligations).Should().Be(expected);
    }

    private static Obligation CreateObligation(int accepted, int obligated, string status = ObligationStatus.NotMet) =>
        ObligationFixture
            .Default()
            .With(x => x.Status, status)
            .With(
                x => x.Tonnages,
                ObligationTonnagesFixture
                    .Default()
                    .With(x => x.Accepted, accepted)
                    .With(x => x.Obligated, obligated)
                    .Create()
            )
            .Create();

    [Theory]
    [InlineData(1, 200, 1)]
    [InlineData(1, 201, 0)]
    [InlineData(1, 199, 1)]
    public void Calculate_WhenPercentageIsNearHalfOfOnePercent_ShouldRoundAwayFromZeroAtMidpoint(
        int accepted,
        int obligated,
        decimal expected
    )
    {
        ObligationCoveragePercentageCalculator
            .CalculateFromObligations([CreateObligation(accepted, obligated)])
            .Should()
            .Be(expected);
    }

    [Theory]
    [InlineData(67, 200, 34)]
    [InlineData(167, 500, 33)]
    [InlineData(168, 500, 34)]
    public void Calculate_WhenPercentageIsNearThirtyThreeAndAHalf_ShouldRoundAwayFromZeroAtMidpoint(
        int accepted,
        int obligated,
        decimal expected
    )
    {
        ObligationCoveragePercentageCalculator
            .CalculateFromObligations([CreateObligation(accepted, obligated)])
            .Should()
            .Be(expected);
    }

    [Theory]
    [InlineData(101, 200, 51)]
    [InlineData(100, 201, 50)]
    [InlineData(101, 199, 51)]
    public void Calculate_WhenPercentageIsNearFiftyAndAHalf_ShouldRoundAwayFromZeroAtMidpoint(
        int accepted,
        int obligated,
        decimal expected
    )
    {
        ObligationCoveragePercentageCalculator
            .CalculateFromObligations([CreateObligation(accepted, obligated)])
            .Should()
            .Be(expected);
    }
}
