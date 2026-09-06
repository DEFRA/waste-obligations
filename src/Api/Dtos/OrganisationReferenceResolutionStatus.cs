namespace Defra.WasteObligations.Api.Dtos;

public record OrganisationReferenceResolutionStatus
{
    public required string State { get; init; }
    public required int Count { get; init; }
}
