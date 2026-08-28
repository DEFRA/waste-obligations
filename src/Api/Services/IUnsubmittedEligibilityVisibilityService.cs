using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services;

public interface IUnsubmittedEligibilityVisibilityService
{
    Task<IReadOnlyList<OrganisationComplianceDeclarationEligibility>> Apply(
        IReadOnlyList<OrganisationComplianceDeclarationEligibility> rows,
        DateTime utcNow,
        CancellationToken cancellationToken
    );

    Task Refresh(
        IClientSessionHandle transactionSession,
        IReadOnlyCollection<ComplianceDeclaration> declarations,
        DateTime utcNow,
        CancellationToken cancellationToken
    );
}
