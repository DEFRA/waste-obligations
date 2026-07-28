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
        var obligationsArray = obligations as Obligation[] ?? obligations.ToArray();
        var totalAccepted = obligationsArray.Sum(o => o.Tonnages.Accepted);
        var totalObligated = obligationsArray.Sum(o => o.Tonnages.Obligated);

        return Calculate(totalAccepted, totalObligated);
    }

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
