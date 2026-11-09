namespace Defra.WasteObligations.Api.Services.ObligationCalculations;

public class ObligationCalculationOptions
{
    public const string SectionName = "ObligationCalculation";

    public Dictionary<int, Dictionary<string, double>> Targets { get; set; } = [];
}
