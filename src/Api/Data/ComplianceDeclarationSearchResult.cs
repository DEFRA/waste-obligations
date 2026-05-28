using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Data;

public record ComplianceDeclarationSearchResult
{
    public IEnumerable<ComplianceDeclaration> ComplianceDeclarations { get; init; } = [];

    public int Total { get; init; }
};
