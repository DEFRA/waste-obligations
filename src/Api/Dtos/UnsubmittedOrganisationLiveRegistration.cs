namespace Defra.WasteObligations.Api.Dtos;

public record UnsubmittedOrganisationLiveRegistration
{
    public required string Status { get; init; }
    public required string Type { get; init; }
    public int RegistrationYear { get; init; }
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset Updated { get; init; }
}
