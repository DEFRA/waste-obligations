using System.ComponentModel.DataAnnotations;

namespace Defra.WasteObligations.Api.Services.ObligationCalculations;

public record ObligationCalculationProcessingOptions
{
    public const string SectionName = "ObligationCalculationProcessing";

    public bool Enabled { get; init; }

    [Range(1, 3600)]
    public int PollIntervalSeconds { get; init; } = 10;
}
