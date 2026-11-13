using System.Numerics;
using Defra.WasteObligations.Api.Dtos;

namespace Defra.WasteObligations.Api.Data.Entities;

public static class ObligationCoveragePercentageCalculator
{
    private const int MaxPercentage = 100;

    public static decimal CalculateFromObligations(IEnumerable<Obligation> obligations)
    {
        return CalculateFromMaterials(
            obligations.Select(x => (x.Material, x.Tonnages.Accepted, x.Tonnages.Obligated, x.Status))
        );
    }

    public static decimal CalculateFromMaterials(
        IEnumerable<(string Material, int Accepted, int Obligated, string Status)> materials
    )
    {
        var totalNumerator = BigInteger.Zero;
        var totalDenominator = BigInteger.One;
        var count = 0;
        var anyNotMet = false;
        var participating = materials.Where(x => x.Status != ObligationStatus.NoDataYet).ToArray();
        anyNotMet = participating.Any(x => x.Status == ObligationStatus.NotMet);
        var groups = participating
            .Select((x, index) => (Value: x, Key: x.Material is Material.Glass or Material.GlassRemelt ? -1 : index))
            .GroupBy(x => x.Key, x => x.Value);
        foreach (var group in groups)
        {
            var accepted = group.Aggregate(BigInteger.Zero, (sum, x) => sum + x.Accepted);
            var obligated = group.Aggregate(BigInteger.Zero, (sum, x) => sum + x.Obligated);
            BigInteger numerator;
            BigInteger denominator;
            if (obligated > 0)
            {
                numerator = BigInteger.Min(accepted, obligated) * MaxPercentage;
                denominator = obligated;
            }
            else
            {
                numerator = group.All(x => x.Status == ObligationStatus.Met) ? MaxPercentage : 0;
                denominator = 1;
            }

            // Keep exact fractions so decimal division cannot move a true midpoint below its rounding boundary.
            totalNumerator = totalNumerator * denominator + numerator * totalDenominator;
            totalDenominator *= denominator;
            var divisor = BigInteger.GreatestCommonDivisor(totalNumerator, totalDenominator);
            totalNumerator /= divisor;
            totalDenominator /= divisor;
            count++;
        }
        if (count == 0)
            return 0m;

        var averageDenominator = totalDenominator * count;
        var rounded = BigInteger.DivRem(totalNumerator, averageDenominator, out var remainder);
        if (BigInteger.Abs(remainder) * 2 >= averageDenominator)
            rounded += totalNumerator.Sign;

        return (decimal)(anyNotMet ? BigInteger.Min(rounded, MaxPercentage - 1) : rounded);
    }
}
