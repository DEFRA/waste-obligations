using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Data;

public record ComplianceDeclarationSearchQuery
{
    public int? ObligationYear { get; init; }
    public ComplianceDeclarationStatus[]? Status { get; init; }
    public RegistrationType[]? RegistrationType { get; init; }
    public string? OrganisationName { get; init; }
}
