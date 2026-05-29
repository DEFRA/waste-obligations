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

    Task<IEnumerable<ComplianceDeclaration>> Read(
        Guid organisationId,
        int obligationYear,
        CancellationToken cancellationToken
    );

    Task<ComplianceDeclarationSearchResult> Search(
        int? obligationYear,
        ComplianceDeclarationStatus[]? status,
        RegistrationType[]? registrationType,
        string? organisationName,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    );

    Task<ComplianceDeclaration> Update(
        ComplianceDeclaration complianceDeclaration,
        CancellationToken cancellationToken
    );
}
