using System.Numerics;
using Defra.WasteObligations.Api.Dtos;

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
        var total = (Numerator: BigInteger.Zero, Denominator: BigInteger.One);
        var count = 0;
        var anyNotMet = false;
        foreach (var material in materials.Where(x => x.Status != ObligationStatus.NoDataYet))
        {
            anyNotMet |= material.Status == ObligationStatus.NotMet;
            total = Add(total, MaterialShare(material.Accepted, material.Obligated, material.Status));
            count++;
        }

        if (count == 0)
            return 0m;

        var denominator = total.Denominator * count;
        var rounded = BigInteger.DivRem(total.Numerator, denominator, out var remainder);
        if (BigInteger.Abs(remainder) * 2 >= denominator)
            rounded += total.Numerator.Sign;

        return (decimal)(anyNotMet ? BigInteger.Min(rounded, MaxPercentage - 1) : rounded);
    }

    private static (BigInteger Numerator, BigInteger Denominator) MaterialShare(
        int accepted,
        int obligated,
        string status
    )
    {
        // Met can include remelt surplus not shown in accepted tonnage.
        if (status == ObligationStatus.Met)
            return (MaxPercentage, 1);

        if (obligated <= 0)
            return (0, 1);

        return ((BigInteger)Math.Min(accepted, obligated) * MaxPercentage, obligated);
    }

    private static (BigInteger Numerator, BigInteger Denominator) Add(
        (BigInteger Numerator, BigInteger Denominator) total,
        (BigInteger Numerator, BigInteger Denominator) share
    )
    {
        // Preserve exact fractions so division cannot move a midpoint below its rounding boundary.
        var numerator = total.Numerator * share.Denominator + share.Numerator * total.Denominator;
        var denominator = total.Denominator * share.Denominator;
        var divisor = BigInteger.GreatestCommonDivisor(numerator, denominator);

        return (numerator / divisor, denominator / divisor);
    }
}
