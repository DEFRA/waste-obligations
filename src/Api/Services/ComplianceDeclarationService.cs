using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Defra.WasteObligations.Api.Services;

public class ComplianceDeclarationService(IDbContext dbContext, ILogger<ComplianceDeclarationService> logger)
    : IComplianceDeclarationService
{
    public async Task<ComplianceDeclaration> Create(
        ComplianceDeclaration complianceDeclaration,
        CancellationToken cancellationToken
    )
    {
        var utcNow = GetUtcNowNoMicroseconds();
        complianceDeclaration = complianceDeclaration with { Version = 1, Created = utcNow, Updated = utcNow };

        await dbContext.ComplianceDeclarations.InsertOneAsync(
            complianceDeclaration,
            cancellationToken: cancellationToken
        );

        logger.LogInformation(
            "Created compliance declaration with id '{ComplianceDeclarationId}'",
            complianceDeclaration.Id
        );

        return complianceDeclaration;
    }

    public async Task<ComplianceDeclaration?> Read(Guid id, CancellationToken cancellationToken) =>
        await dbContext
            .ComplianceDeclarations.Find(Builders<ComplianceDeclaration>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);

    public async Task<IEnumerable<ComplianceDeclaration>> Read(
        Guid organisationId,
        int obligationYear,
        CancellationToken cancellationToken
    ) =>
        await dbContext
            .ComplianceDeclarations.AsQueryable()
            .Where(x => x.Organisation.Id == organisationId && x.ObligationYear == obligationYear)
            .ToListAsync(cancellationToken);

    private static DateTime GetUtcNowNoMicroseconds()
    {
        var now = DateTimeOffset.UtcNow;
        now = new DateTimeOffset(now.Ticks - now.Ticks % TimeSpan.TicksPerMillisecond, TimeSpan.Zero);

        return now.UtcDateTime;
    }
}
