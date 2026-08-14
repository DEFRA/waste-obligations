namespace Defra.WasteObligations.Api.Data;

public record ComplianceDeclarationSort
{
    public ComplianceDeclarationSortField Field { get; init; }
    public ComplianceDeclarationSortDirection Direction { get; init; }
}

public enum ComplianceDeclarationSortField
{
    RecyclingObligations,
    PercentageMet,
    DateSubmitted,
    Regulation43,
    OrganisationName,
    OrganisationId,
}

public enum ComplianceDeclarationSortDirection
{
    Ascending,
    Descending,
}
