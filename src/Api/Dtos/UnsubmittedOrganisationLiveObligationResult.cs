namespace Defra.WasteObligations.Api.Dtos;

public record UnsubmittedOrganisationLiveObligationResult
{
    public int ObligationYear { get; init; }
    public required UnsubmittedOrganisationRawObligation[] RawObligations { get; init; }
    public required UnsubmittedOrganisationLiveObligationCalculation UnsubmittedCalculation { get; init; }
}
