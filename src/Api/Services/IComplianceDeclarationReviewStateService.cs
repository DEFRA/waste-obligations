using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services;

public interface IComplianceDeclarationReviewStateService
{
    Task Refresh(
        IClientSessionHandle transactionSession,
        IReadOnlyCollection<ComplianceDeclaration> declarations,
        DateTime utcNow,
        CancellationToken cancellationToken
    );
}
