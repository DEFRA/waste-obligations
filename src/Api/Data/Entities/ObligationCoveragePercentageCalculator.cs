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
        var hasGlass = false;
        var glassAccepted = BigInteger.Zero;
        var glassObligated = BigInteger.Zero;
        var remeltAccepted = BigInteger.Zero;
        var remeltObligated = BigInteger.Zero;
        var glassAllMet = true;

        foreach (var material in materials.Where(x => x.Status != ObligationStatus.NoDataYet))
        {
            anyNotMet |= material.Status == ObligationStatus.NotMet;
            if (material.Material is Material.Glass or Material.GlassRemelt)
            {
                hasGlass = true;
                if (material.Material == Material.GlassRemelt)
                {
                    remeltAccepted += material.Accepted;
                    remeltObligated += material.Obligated;
                }
                else
                {
                    glassAccepted += material.Accepted;
                    glassObligated += material.Obligated;
                }
                glassAllMet &= material.Status == ObligationStatus.Met;
            }
            else
            {
                AddCoverage(material.Accepted, material.Obligated, material.Status == ObligationStatus.Met);
                count++;
            }
        }

        if (hasGlass)
        {
            // Remelt surplus can meet remaining glass; remaining-glass surplus cannot meet remelt.
            var remeltCovered = BigInteger.Min(remeltAccepted, remeltObligated);
            var remeltSurplus = BigInteger.Max(BigInteger.Zero, remeltAccepted - remeltObligated);
            var glassCovered = BigInteger.Min(glassAccepted + remeltSurplus, glassObligated);
            AddCoverage(remeltCovered + glassCovered, remeltObligated + glassObligated, glassAllMet);
            count++;
        }

        void AddCoverage(BigInteger accepted, BigInteger obligated, bool met)
        {
            var numerator = met ? new BigInteger(MaxPercentage) : BigInteger.Zero;
            if (obligated > 0)
                numerator = BigInteger.Min(accepted, obligated) * MaxPercentage;
            var denominator = obligated > 0 ? obligated : BigInteger.One;

            // Keep exact fractions so decimal division cannot move a true midpoint below its rounding boundary.
            totalNumerator = totalNumerator * denominator + numerator * totalDenominator;
            totalDenominator *= denominator;
            var divisor = BigInteger.GreatestCommonDivisor(totalNumerator, totalDenominator);
            totalNumerator /= divisor;
            totalDenominator /= divisor;
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
