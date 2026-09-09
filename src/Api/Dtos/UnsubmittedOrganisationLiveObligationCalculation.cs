namespace Defra.WasteObligations.Api.Dtos;

public record UnsubmittedOrganisationLiveObligationCalculation
{
    public int ObligationCount { get; init; }
    public int TotalAcceptedTonnage { get; init; }
    public int TotalObligatedTonnage { get; init; }
    public bool? RecyclingObligationsMet { get; init; }
    public decimal ObligationCoveragePercentage { get; init; }
    public required string SourceFingerprint { get; init; }
}
