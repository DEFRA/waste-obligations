namespace Defra.WasteObligations.Api.Services.OrganisationEligibility;

public record OrganisationEligibilitySnapshotContent
{
    public required IReadOnlyList<Data.Entities.OrganisationEligibility> Rows { get; init; }
    public required string Fingerprint { get; init; }
}
