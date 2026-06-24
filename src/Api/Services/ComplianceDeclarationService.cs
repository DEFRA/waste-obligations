using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Defra.WasteObligations.Api.Services;

public class ComplianceDeclarationService(
    IDbContext dbContext,
    ILogger<ComplianceDeclarationService> logger,
    TimeProvider timeProvider,
    IEventIdGenerator eventIdGenerator
) : IComplianceDeclarationService
{
    private const string Actor = "service:waste-obligations";
    private const string CounterId = "audit_event";
    private const string Entity = "compliance_declaration";
    private const string InsertOperation = "insert";
    private const string UpdateOperation = "update";
    private const string DeleteOperation = "delete";
    private const string SchemaVersion = "compliance_declaration.v1";

    public async Task<ComplianceDeclaration> Create(
        ComplianceDeclaration complianceDeclaration,
        CancellationToken cancellationToken
    )
    {
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        complianceDeclaration = complianceDeclaration with { Version = 1, Created = utcNow, Updated = utcNow };

        using var session = await dbContext.StartSession(cancellationToken);
        session.StartTransaction();

        try
        {
            await dbContext.ComplianceDeclarations.InsertOneAsync(
                session,
                complianceDeclaration,
                cancellationToken: cancellationToken
            );

            await InsertAuditEvent(
                session,
                InsertOperation,
                complianceDeclaration.Id.ToString(),
                complianceDeclaration.Version,
                null,
                complianceDeclaration.ToBsonDocument(),
                utcNow,
                cancellationToken
            );

            await session.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await session.AbortTransactionAsync(CancellationToken.None);
            throw;
        }

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
        using var session = await dbContext.StartSession(cancellationToken);
        session.StartTransaction();

        try
        {
            var objectId = ObjectId.Parse(id);
            var current = await dbContext
                .ComplianceDeclarations.Find(session, Builders<ComplianceDeclaration>.Filter.Eq(x => x.Id, objectId))
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);

            if (current is null)
            {
                await session.AbortTransactionAsync(cancellationToken);

                return false;
            }

            var deleteFilter = Builders<ComplianceDeclaration>.Filter.And(
                Builders<ComplianceDeclaration>.Filter.Eq(x => x.Id, objectId),
                Builders<ComplianceDeclaration>.Filter.Eq(x => x.Version, current.Version)
            );

            var deleteResult = await dbContext.ComplianceDeclarations.DeleteOneAsync(
                session,
                deleteFilter,
                null,
                cancellationToken
            );

            if (deleteResult.DeletedCount == 0)
                throw new ConcurrencyException(
                    $"Concurrency issue on delete, compliance declaration with id '{current.Id}' was not deleted"
                );

            var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
            await InsertAuditEvent(
                session,
                DeleteOperation,
                current.Id.ToString(),
                current.Version + 1,
                current.ToBsonDocument(),
                null,
                utcNow,
                cancellationToken
            );

            await session.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await session.AbortTransactionAsync(CancellationToken.None);
            throw;
        }

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
        ComplianceDeclaration current,
        ComplianceDeclaration updated,
        CancellationToken cancellationToken
    )
    {
        using var session = await dbContext.StartSession(cancellationToken);
        session.StartTransaction();

        var filter = Builders<ComplianceDeclaration>.Filter.And(
            Builders<ComplianceDeclaration>.Filter.Eq(x => x.Id, current.Id),
            Builders<ComplianceDeclaration>.Filter.Eq(x => x.Version, current.Version)
        );

        updated = updated with { Version = current.Version + 1, Updated = timeProvider.GetUtcNowWithoutMicroseconds() };

        try
        {
            var replaceOneResult = await dbContext.ComplianceDeclarations.ReplaceOneAsync(
                session,
                filter,
                updated,
                new ReplaceOptions { IsUpsert = false },
                cancellationToken: cancellationToken
            );

            if (replaceOneResult.ModifiedCount == 0)
                throw new ConcurrencyException(
                    $"Concurrency issue on write, compliance declaration with id '{current.Id}' was not updated"
                );

            await InsertAuditEvent(
                session,
                UpdateOperation,
                updated.Id.ToString(),
                updated.Version,
                current.ToBsonDocument(),
                updated.ToBsonDocument(),
                updated.Updated,
                cancellationToken
            );

            await session.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await session.AbortTransactionAsync(CancellationToken.None);
            throw;
        }

        logger.LogInformation("Updated compliance declaration with id '{ComplianceDeclarationId}'", updated.Id);

        return updated;
    }

    private async Task InsertAuditEvent(
        IClientSessionHandle session,
        string operation,
        string entityId,
        int version,
        BsonDocument? before,
        BsonDocument? after,
        DateTime occurredAt,
        CancellationToken cancellationToken
    )
    {
        var sequence = await AllocateSequence(session, cancellationToken);
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();

        await dbContext.AuditEvents.InsertOneAsync(
            session,
            new AuditEvent
            {
                EventId = eventIdGenerator.Generate(),
                Sequence = sequence,
                Entity = Entity,
                EntityId = entityId,
                Operation = operation,
                OccurredAt = occurredAt,
                RecordedAt = utcNow,
                Actor = Actor,
                Version = version,
                Before = before,
                After = after,
                SchemaVersion = SchemaVersion,
            },
            cancellationToken: cancellationToken
        );
    }

    private async Task<long> AllocateSequence(IClientSessionHandle session, CancellationToken cancellationToken)
    {
        var counter = await dbContext.AuditEventCounters.FindOneAndUpdateAsync(
            session,
            Builders<AuditEventCounter>.Filter.Eq(x => x.Id, CounterId),
            Builders<AuditEventCounter>.Update.Inc(x => x.Sequence, 1),
            new FindOneAndUpdateOptions<AuditEventCounter> { IsUpsert = true, ReturnDocument = ReturnDocument.After },
            cancellationToken
        );

        return counter.Sequence;
    }
}
