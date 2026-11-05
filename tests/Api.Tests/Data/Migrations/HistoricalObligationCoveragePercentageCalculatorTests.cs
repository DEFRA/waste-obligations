using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Migrations;
using Defra.WasteObligations.Api.Dtos;
using MongoDB.Bson;

namespace Defra.WasteObligations.Api.Tests.Data.Migrations;

public class HistoricalObligationCoveragePercentageCalculatorTests
{
    [Fact]
    public void Calculate_WhenTotalObligatedIsZero_ShouldReturnZero()
    {
        HistoricalObligationCoveragePercentageCalculator.Calculate(10, 0).Should().Be(0m);
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
        HistoricalObligationCoveragePercentageCalculator.Calculate(accepted, obligated).Should().Be(expected);
    }

    [Fact]
    public void CalculateFromBsonObligations_WhenJiraAcceptanceCriteriaExamplesProvided_ShouldReturnExpectedWholeNumber()
    {
        var obligations = new BsonArray { CreateBsonObligation(accepted: 850, obligated: 925) };

        HistoricalObligationCoveragePercentageCalculator.CalculateFromBsonObligations(obligations).Should().Be(92m);
    }

    [Fact]
    public void CalculateFromBsonObligations_WhenAcceptedExceedsObligatedOnOneMaterial_ShouldCapAtOneHundred()
    {
        var obligations = new BsonArray
        {
            CreateBsonObligation(accepted: 100, obligated: 50),
            CreateBsonObligation(accepted: 0, obligated: 50),
        };

        HistoricalObligationCoveragePercentageCalculator.CalculateFromBsonObligations(obligations).Should().Be(100m);
    }

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
        HistoricalObligationCoveragePercentageCalculator.Calculate(accepted, obligated).Should().Be(expected);
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
        HistoricalObligationCoveragePercentageCalculator.Calculate(accepted, obligated).Should().Be(expected);
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
        HistoricalObligationCoveragePercentageCalculator.Calculate(accepted, obligated).Should().Be(expected);
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
}
