using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Services;

public interface IComplianceDeclarationService
{
    Task<ComplianceDeclaration> Create(
        ComplianceDeclaration complianceDeclaration,
        CancellationToken cancellationToken
    );

    Task<ComplianceDeclaration?> Read(string id, CancellationToken cancellationToken);

    Task<ComplianceDeclarationPageResult> Read(
        Guid organisationId,
        int obligationYear,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    );

    Task<bool> Delete(string id, CancellationToken cancellationToken);

    Task<ComplianceDeclarationPageResult> Search(
        ComplianceDeclarationSearchQuery query,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    );

    Task<ComplianceDeclaration> Update(
        ComplianceDeclaration current,
        ComplianceDeclaration updated,
        CancellationToken cancellationToken
    );
}
