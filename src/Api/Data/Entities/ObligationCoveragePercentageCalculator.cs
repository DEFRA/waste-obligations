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
        var glassMaterials = new List<(string Material, int Accepted, int Obligated, string Status)>();

        foreach (var material in materials.Where(x => x.Status != ObligationStatus.NoDataYet))
        {
            anyNotMet |= material.Status == ObligationStatus.NotMet;
            if (material.Material is Material.Glass or Material.GlassRemelt)
            {
                glassMaterials.Add(material);
            }
            else
            {
                AddCoverage(material.Accepted, material.Obligated, material.Status == ObligationStatus.Met);
                count++;
            }
        }

        if (glassMaterials.Count > 0)
        {
            var glass = CalculateGlassCoverage(glassMaterials);
            AddCoverage(glass.Accepted, glass.Obligated, glass.AllMet);
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

    private static (BigInteger Accepted, BigInteger Obligated, bool AllMet) CalculateGlassCoverage(
        IEnumerable<(string Material, int Accepted, int Obligated, string Status)> materials
    )
    {
        var glassAccepted = BigInteger.Zero;
        var glassObligated = BigInteger.Zero;
        var remeltAccepted = BigInteger.Zero;
        var remeltObligated = BigInteger.Zero;
        var allMet = true;
        foreach (var material in materials)
        {
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
            allMet &= material.Status == ObligationStatus.Met;
        }

        // Remelt surplus can meet remaining glass; remaining-glass surplus cannot meet remelt.
        var remeltCovered = BigInteger.Min(remeltAccepted, remeltObligated);
        var remeltSurplus = BigInteger.Max(BigInteger.Zero, remeltAccepted - remeltObligated);
        var glassCovered = BigInteger.Min(glassAccepted + remeltSurplus, glassObligated);

        return (remeltCovered + glassCovered, remeltObligated + glassObligated, allMet);
    }
}
