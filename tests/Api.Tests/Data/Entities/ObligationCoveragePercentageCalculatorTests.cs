using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Tests.Data.Entities;

public class ObligationCoveragePercentageCalculatorTests
{
    [Fact]
    public void Calculate_WhenTotalObligatedIsZero_ShouldReturnZero()
    {
        ObligationCoveragePercentageCalculator.Calculate(10, 0).Should().Be(0m);
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
