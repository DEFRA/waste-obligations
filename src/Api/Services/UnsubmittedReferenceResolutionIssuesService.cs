using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Dtos;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services;

public class UnsubmittedReferenceResolutionIssuesService(IDbContext dbContext)
    : IUnsubmittedReferenceResolutionIssuesService
{
    public async Task<UnsubmittedReferenceResolutionIssues> Get(CancellationToken cancellationToken)
    {
        var snapshot = await dbContext
            .OrganisationEligibilitySnapshots.Find(x =>
                x.Id == Data.Entities.OrganisationEligibilitySnapshot.SnapshotId
            )
            .SingleOrDefaultAsync(cancellationToken);
        if (snapshot?.ActiveGeneration is not { } activeGeneration)
        {
            return new UnsubmittedReferenceResolutionIssues { ActiveGeneration = null, ReferenceResolutionIssues = [] };
        }

        var issueRows = await dbContext
            .OrganisationComplianceDeclarationEligibilities.Find(x =>
                x.Generation == activeGeneration
                && x.ReferenceNumberResolutionState != Data.Entities.OrganisationReferenceNumberResolutionState.Resolved
            )
            .SortBy(x => x.ReferenceNumberResolutionState)
            .ThenBy(x => x.OrganisationId)
            .ThenBy(x => x.ObligationYear)
            .ThenBy(x => x.RegistrationType)
            .ToListAsync(cancellationToken);

        return new UnsubmittedReferenceResolutionIssues
        {
            ActiveGeneration = activeGeneration,
            ReferenceResolutionIssues = [.. issueRows.Select(ToDto)],
        };
    }

    private static UnsubmittedReferenceResolutionIssue ToDto(
        Data.Entities.OrganisationComplianceDeclarationEligibility issue
    ) =>
        new()
        {
            OrganisationId = issue.OrganisationId,
            ObligationYear = issue.ObligationYear,
            RegistrationType = issue.RegistrationType switch
            {
                Data.Entities.RegistrationType.DirectProducer => RegistrationType.DirectProducer,
                Data.Entities.RegistrationType.ComplianceScheme => RegistrationType.ComplianceScheme,
                _ => throw new ArgumentOutOfRangeException(nameof(issue)),
            },
            RegistrationStatus = issue.RegistrationStatus.ToString(),
            Name = issue.Name,
            TradingName = issue.TradingName,
            CompaniesHouseNumber = issue.CompaniesHouseNumber,
            Country = issue.BusinessCountry,
            ReferenceResolutionState = issue.ReferenceNumberResolutionState.ToString(),
        };
}
