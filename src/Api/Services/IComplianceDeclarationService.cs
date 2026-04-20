using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Services;

public interface IComplianceDeclarationService
{
    Task<ComplianceDeclaration> Create(
        ComplianceDeclaration complianceDeclaration,
        CancellationToken cancellationToken
    );

    Task<ComplianceDeclaration?> Read(Guid id, CancellationToken cancellationToken);
}
