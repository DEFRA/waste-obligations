using MongoDB.Bson;

namespace Defra.WasteObligations.Api.Data.Entities;

public static class ObligationCoveragePercentageCalculator
{
    private const int DecimalPlaces = 0;
    private const decimal MaxPercentage = 100m;
    private const string TonnagesField = "tonnages";
    private const string AcceptedField = "accepted";
    private const string ObligatedField = "obligated";

    public static decimal CalculateFromObligations(IEnumerable<Obligation> obligations)
    {
        return CalculateFromMaterials(obligations.Select(x => (x.Tonnages.Accepted, x.Tonnages.Obligated, x.Status)));
    }

    public static decimal CalculateFromMaterials(IEnumerable<(int Accepted, int Obligated, string Status)> materials)
    {
        var percentages = materials
            .Where(x => x.Status != Dtos.ObligationStatus.NoDataYet)
            .Select(x =>
                x.Obligated > 0 ? Math.Min((decimal)x.Accepted / x.Obligated * MaxPercentage, MaxPercentage)
                : x.Accepted == 0 && x.Status == Dtos.ObligationStatus.Met ? MaxPercentage
                : 0m
            )
            .ToArray();

        return percentages.Length == 0
            ? 0m
            : Math.Round(percentages.Average(), DecimalPlaces, MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateIndividualMaterialsFromBsonObligations(BsonArray obligations)
    {
        return CalculateFromMaterials(
            obligations.Select(x =>
            {
                var document = x.AsBsonDocument;
                var tonnages = document[TonnagesField].AsBsonDocument;

                return (
                    tonnages[AcceptedField].ToInt32(),
                    tonnages[ObligatedField].ToInt32(),
                    document["status"].AsString
                );
            })
        );
    }

    // Migrations 004 and 005 retain the historical aggregate-tonnage calculation.
    public static decimal CalculateFromBsonObligations(BsonArray obligations)
    {
        var totalAccepted = 0;
        var totalObligated = 0;

        foreach (var obligation in obligations)
        {
            var tonnages = obligation.AsBsonDocument[TonnagesField].AsBsonDocument;
            var accepted = tonnages[AcceptedField].ToInt32();
            var obligated = tonnages[ObligatedField].ToInt32();
            totalAccepted += accepted;
            totalObligated += obligated;
        }

        return Calculate(totalAccepted, totalObligated);
    }

    public static decimal Calculate(int totalAccepted, int totalObligated)
    {
        if (totalObligated == 0)
        {
            return 0m;
        }

        var percentage = (decimal)totalAccepted / totalObligated * 100m;
        var cappedPercentage = Math.Min(percentage, MaxPercentage);

        return Math.Round(cappedPercentage, DecimalPlaces, MidpointRounding.AwayFromZero);
    }
}
