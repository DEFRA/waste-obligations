using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Data;

public interface IDbContext
{
    IMongoCollection<ComplianceDeclaration> ComplianceDeclarations { get; }
    IMongoCollection<OrganisationEligibility> OrganisationEligibilities { get; }
    IMongoCollection<OrganisationEligibilitySnapshot> OrganisationEligibilitySnapshots { get; }
    IMongoCollection<OrganisationEligibilityRefreshLease> OrganisationEligibilityRefreshLeases { get; }
    IMongoCollection<OrganisationReferenceCache> OrganisationReferenceCaches { get; }

    Task<TResult> ExecuteTransaction<TResult>(
        Func<IClientSessionHandle, CancellationToken, Task<TResult>> callback,
        string transactionName,
        CancellationToken cancellationToken
    );
}
