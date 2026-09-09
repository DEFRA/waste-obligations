namespace Defra.WasteObligations.Api.Dtos;

public record UnsubmittedOrganisationLiveData
{
    public UnsubmittedOrganisationLiveOrganisation? Organisation { get; init; }
    public required UnsubmittedOrganisationLiveObligationResult[] ObligationResults { get; init; }
}
