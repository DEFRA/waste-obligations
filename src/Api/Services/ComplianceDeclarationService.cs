using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Defra.WasteObligations.Api.Services;

public class ComplianceDeclarationService(
    IDbContext dbContext,
    ILogger<ComplianceDeclarationService> logger,
    TimeProvider timeProvider
) : IComplianceDeclarationService
{
    public async Task<ComplianceDeclaration> Create(
        ComplianceDeclaration complianceDeclaration,
        CancellationToken cancellationToken
    )
    {
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
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

    public async Task<ComplianceDeclaration?> Read(string id, CancellationToken cancellationToken) =>
        await dbContext
            .ComplianceDeclarations.Find(Builders<ComplianceDeclaration>.Filter.Eq(x => x.Id, ObjectId.Parse(id)))
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

    public async Task<bool> Delete(string id, CancellationToken cancellationToken)
    {
        var deleteResult = await dbContext.ComplianceDeclarations.DeleteOneAsync(
            Builders<ComplianceDeclaration>.Filter.Eq(x => x.Id, ObjectId.Parse(id)),
            cancellationToken
        );

        if (deleteResult.DeletedCount == 0)
            return false;

        logger.LogInformation("Deleted compliance declaration with id '{ComplianceDeclarationId}'", id);

        return true;
    }

    public async Task<ComplianceDeclarationSearchResult> Search(
        ComplianceDeclarationSearchQuery query,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        var filters = new List<FilterDefinition<ComplianceDeclaration>>();

        if (query.ObligationYear.HasValue)
        {
            filters.Add(Builders<ComplianceDeclaration>.Filter.Eq(x => x.ObligationYear, query.ObligationYear.Value));
        }

        if (query.Status is { Length: > 0 })
        {
            filters.Add(Builders<ComplianceDeclaration>.Filter.In(x => x.Status, query.Status));
        }

        if (query.RegistrationType is { Length: > 0 })
        {
            filters.Add(
                Builders<ComplianceDeclaration>.Filter.In(x => x.Organisation.RegistrationType, query.RegistrationType)
            );
        }

        if (!string.IsNullOrWhiteSpace(query.OrganisationName))
        {
            filters.Add(
                Builders<ComplianceDeclaration>.Filter.Regex(
                    x => x.Organisation.Name,
                    new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(query.OrganisationName), "i")
                )
            );
        }

        var combinedFilter =
            filters.Count == 0
                ? Builders<ComplianceDeclaration>.Filter.Empty
                : Builders<ComplianceDeclaration>.Filter.And(filters);

        var countTask = dbContext.ComplianceDeclarations.CountDocumentsAsync(
            combinedFilter,
            cancellationToken: cancellationToken
        );
        var resultsTask = dbContext
            .ComplianceDeclarations.Find(combinedFilter)
            .SortBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        await Task.WhenAll(countTask, resultsTask);

        return new ComplianceDeclarationSearchResult
        {
            ComplianceDeclarations = resultsTask.Result,
            Total = (int)countTask.Result,
        };
    }

    public async Task<ComplianceDeclaration> Update(
        ComplianceDeclaration complianceDeclaration,
        CancellationToken cancellationToken
    )
    {
        var filter = Builders<ComplianceDeclaration>.Filter.And(
            Builders<ComplianceDeclaration>.Filter.Eq(x => x.Id, complianceDeclaration.Id),
            Builders<ComplianceDeclaration>.Filter.Eq(x => x.Version, complianceDeclaration.Version)
        );

        complianceDeclaration = complianceDeclaration with
        {
            Version = complianceDeclaration.Version + 1,
            Updated = timeProvider.GetUtcNowWithoutMicroseconds(),
        };

        var replaceOneResult = await dbContext.ComplianceDeclarations.ReplaceOneAsync(
            filter,
            complianceDeclaration,
            new ReplaceOptions { IsUpsert = false },
            cancellationToken: cancellationToken
        );

        if (replaceOneResult.ModifiedCount == 0)
            throw new ConcurrencyException(
                $"Concurrency issue on write, compliance declaration with id '{complianceDeclaration.Id}' was not updated"
            );

        logger.LogInformation(
            "Updated compliance declaration with id '{ComplianceDeclarationId}'",
            complianceDeclaration.Id
        );

        return complianceDeclaration;
    }
}
