using System.Numerics;

namespace Defra.WasteObligations.Api.Data.Entities;

public static class ObligationCoveragePercentageCalculator
{
    private const int MaxPercentage = 100;

    public static decimal CalculateFromObligations(IEnumerable<Obligation> obligations)
    {
        return CalculateFromMaterials(obligations.Select(x => (x.Tonnages.Accepted, x.Tonnages.Obligated, x.Status)));
    }

    public static decimal CalculateFromMaterials(IEnumerable<(int Accepted, int Obligated, string Status)> materials)
    {
        var totalNumerator = BigInteger.Zero;
        var totalDenominator = BigInteger.One;
        var count = 0;
        var anyNotMet = false;
        foreach (var material in materials.Where(x => x.Status != Dtos.ObligationStatus.NoDataYet))
        {
            anyNotMet |= material.Status == Dtos.ObligationStatus.NotMet;
            BigInteger numerator;
            int denominator;
            if (material.Obligated > 0)
            {
                numerator = (BigInteger)Math.Min(material.Accepted, material.Obligated) * MaxPercentage;
                denominator = material.Obligated;
            }
            else
            {
                numerator = material.Status == Dtos.ObligationStatus.Met ? MaxPercentage : 0;
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
