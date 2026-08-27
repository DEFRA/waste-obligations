using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services;

public class ComplianceDeclarationReviewStateService(IDbContext dbContext) : IComplianceDeclarationReviewStateService
{
    public async Task Refresh(
        IClientSessionHandle transactionSession,
        IReadOnlyCollection<ComplianceDeclaration> declarations,
        DateTime utcNow,
        CancellationToken cancellationToken
    )
    {
        var keys = declarations.Select(ComplianceDeclarationReviewStateKey.From).Distinct();

        foreach (var key in keys)
        {
            var filter = Builders<ComplianceDeclaration>.Filter.And(
                Builders<ComplianceDeclaration>.Filter.Eq(x => x.Organisation.Id, key.OrganisationId),
                Builders<ComplianceDeclaration>.Filter.Eq(x => x.ObligationYear, key.ObligationYear),
                Builders<ComplianceDeclaration>.Filter.Eq(x => x.Organisation.RegistrationType, key.RegistrationType),
                Builders<ComplianceDeclaration>.Filter.In(
                    x => x.Status,
                    [ComplianceDeclarationStatus.Submitted, ComplianceDeclarationStatus.Accepted]
                )
            );
            var unsubmittedExclusionCount = await dbContext.ComplianceDeclarations.CountDocumentsAsync(
                transactionSession,
                filter,
                cancellationToken: cancellationToken
            );
            var update = Builders<ComplianceDeclarationReviewState>
                .Update.Set(x => x.UnsubmittedExclusionCount, (int)unsubmittedExclusionCount)
                .Set(x => x.UpdatedAt, utcNow)
                .SetOnInsert(x => x.OrganisationId, key.OrganisationId)
                .SetOnInsert(x => x.ObligationYear, key.ObligationYear)
                .SetOnInsert(x => x.RegistrationType, key.RegistrationType);

            await dbContext.ComplianceDeclarationReviewStates.UpdateOneAsync(
                transactionSession,
                x =>
                    x.OrganisationId == key.OrganisationId
                    && x.ObligationYear == key.ObligationYear
                    && x.RegistrationType == key.RegistrationType,
                update,
                new UpdateOptions { IsUpsert = true },
                cancellationToken: cancellationToken
            );
        }
    }
}
