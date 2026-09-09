namespace Defra.WasteObligations.Api.Dtos;

public record OrganisationEligibilityMaterialisedObligationYear
{
    public required int ObligationYear { get; init; }
    public required int RowCount { get; init; }
}
