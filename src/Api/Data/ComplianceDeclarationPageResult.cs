using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Data;

public record ComplianceDeclarationPageResult
{
    public IEnumerable<ComplianceDeclaration> ComplianceDeclarations { get; init; } = [];

    public int Total { get; init; }
};
