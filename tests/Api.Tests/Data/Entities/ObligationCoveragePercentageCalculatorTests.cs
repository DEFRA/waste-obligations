using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using MongoDB.Bson;
using Obligation = Defra.WasteObligations.Api.Data.Entities.Obligation;

namespace Defra.WasteObligations.Api.Tests.Data.Entities;

public class ObligationCoveragePercentageCalculatorTests
{
    [Fact]
    public void Calculate_WhenTotalObligatedIsZero_ShouldReturnZero()
    {
        ObligationCoveragePercentageCalculator.Calculate(10, 0).Should().Be(0m);
    }

    [Theory]
    [InlineData(850, 925, 92)]
    [InlineData(500, 500, 100)]
    [InlineData(1150, 925, 100)]
    [InlineData(107, 200, 54)]
    [InlineData(0, 925, 0)]
    public void Calculate_WhenJiraAcceptanceCriteriaExamplesProvided_ShouldReturnExpectedWholeNumber(
        int accepted,
        int obligated,
        decimal expected
    )
    {
        ObligationCoveragePercentageCalculator.Calculate(accepted, obligated).Should().Be(expected);
    }

    [Fact]
    public void CalculateFromObligations_WhenAcceptedExceedsObligatedOnOneMaterial_ShouldCapAtOneHundred()
    {
        var obligations = new Obligation[]
        {
            ObligationFixture
                .Default()
                .With(
                    x => x.Tonnages,
                    ObligationTonnagesFixture.Default().With(t => t.Accepted, 100).With(t => t.Obligated, 50).Create()
                )
                .Create(),
            ObligationFixture
                .Default()
                .With(x => x.Material, Material.Glass)
                .With(
                    x => x.Tonnages,
                    ObligationTonnagesFixture.Default().With(t => t.Accepted, 0).With(t => t.Obligated, 50).Create()
                )
                .Create(),
        };

        ObligationCoveragePercentageCalculator.CalculateFromObligations(obligations).Should().Be(100m);
    }

    [Fact]
    public void CalculateFromObligations_WhenObligationsAreNotAnArray_ShouldCalculateFromEnumerable()
    {
        var obligations = new List<Obligation>
        {
            ObligationFixture
                .Default()
                .With(
                    x => x.Tonnages,
                    ObligationTonnagesFixture.Default().With(t => t.Accepted, 2).With(t => t.Obligated, 5).Create()
                )
                .Create(),
        };

        ObligationCoveragePercentageCalculator.CalculateFromObligations(obligations).Should().Be(40m);
    }

    [Fact]
    public void CalculateFromBsonObligations_WhenJiraAcceptanceCriteriaExamplesProvided_ShouldReturnExpectedWholeNumber()
    {
        var obligations = new BsonArray { CreateBsonObligation(accepted: 850, obligated: 925) };

        ObligationCoveragePercentageCalculator.CalculateFromBsonObligations(obligations).Should().Be(92m);
    }

    [Fact]
    public void CalculateFromBsonObligations_WhenAcceptedExceedsObligatedOnOneMaterial_ShouldCapAtOneHundred()
    {
        var obligations = new BsonArray
        {
            CreateBsonObligation(accepted: 100, obligated: 50),
            CreateBsonObligation(accepted: 0, obligated: 50),
        };

        ObligationCoveragePercentageCalculator.CalculateFromBsonObligations(obligations).Should().Be(100m);
    }

    private static BsonDocument CreateBsonObligation(int accepted, int obligated) =>
        new()
        {
            ["material"] = Material.Plastic,
            ["recyclingTarget"] = 0.75m,
            ["status"] = ObligationStatus.NoDataYet,
            ["tonnages"] = new BsonDocument
            {
                ["material"] = 100,
                ["awaitingAcceptance"] = 10,
                ["accepted"] = accepted,
                ["outstanding"] = 20,
                ["obligated"] = obligated,
            },
        };

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
        ObligationCoveragePercentageCalculator.Calculate(accepted, obligated).Should().Be(expected);
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
        ObligationCoveragePercentageCalculator.Calculate(accepted, obligated).Should().Be(expected);
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
        ObligationCoveragePercentageCalculator.Calculate(accepted, obligated).Should().Be(expected);
    }
}
