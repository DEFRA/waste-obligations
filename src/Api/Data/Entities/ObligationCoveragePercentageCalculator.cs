namespace Defra.WasteObligations.Api.Data.Entities;

public static class ObligationCoveragePercentageCalculator
{
    private const int DecimalPlaces = 0;
    private const decimal MaxPercentage = 100m;

    public static decimal CalculateFromObligations(IEnumerable<Obligation> obligations)
    {
        return CalculateFromMaterials(obligations.Select(x => (x.Tonnages.Accepted, x.Tonnages.Obligated, x.Status)));
    }

    public static decimal CalculateFromMaterials(IEnumerable<(int Accepted, int Obligated, string Status)> materials)
    {
        var percentages = materials
            .Where(x => x.Status != Dtos.ObligationStatus.NoDataYet)
            .Select(x =>
            {
                if (x.Obligated > 0)
                {
                    return Math.Min((decimal)x.Accepted / x.Obligated * MaxPercentage, MaxPercentage);
                }

                return x.Accepted == 0 && x.Status == Dtos.ObligationStatus.Met ? MaxPercentage : 0m;
            })
            .ToArray();

        return percentages.Length == 0
            ? 0m
            : Math.Round(percentages.Average(), DecimalPlaces, MidpointRounding.AwayFromZero);
    }
}
