using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Utils.Logging;
using Defra.WasteObligations.Api.Utils.Metrics;
using Defra.WasteObligations.AuditEvents;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Defra.WasteObligations.Api.Services;

public class ComplianceDeclarationService(
    IDbContext dbContext,
    ILogger<ComplianceDeclarationService> logger,
    TimeProvider timeProvider,
    IAuditEventService auditEventService,
    IComplianceDeclarationMetrics complianceDeclarationMetrics,
    TraceIdReader traceIdReader,
    IOptions<ComplianceDeclarationOptions> complianceDeclarationOptions
) : IComplianceDeclarationService
{
    private const string Actor = "service:waste-obligations";
    private const string ComplianceDeclarationEntity = "compliance_declaration";
    private readonly TimeSpan _transactionTimeout = TimeSpan.FromSeconds(
        complianceDeclarationOptions.Value.TransactionTimeoutSeconds
    );

    public async Task<ComplianceDeclaration> Create(
        ComplianceDeclaration complianceDeclaration,
        CancellationToken cancellationToken
    )
    {
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        complianceDeclaration = complianceDeclaration with { Version = 1, Created = utcNow, Updated = utcNow };

        using var session = await dbContext.StartSession(cancellationToken);
        await ExecuteTransaction(
            session,
            "create",
            complianceDeclaration.Id,
            async (transactionSession, transactionCancellationToken) =>
            {
                await dbContext.ComplianceDeclarations.InsertOneAsync(
                    transactionSession,
                    complianceDeclaration,
                    cancellationToken: transactionCancellationToken
                );

                await auditEventService.RecordEvent(
                    transactionSession,
                    new AuditEventRequest(
                        Actor,
                        ComplianceDeclarationEntity,
                        AuditEventOperation.Insert,
                        "submission.created",
                        null,
                        complianceDeclaration.Id.ToString(),
                        complianceDeclaration.Version,
                        null,
                        complianceDeclaration.ToBsonDocument(),
                        complianceDeclaration.SchemaVersion,
                        utcNow,
                        traceIdReader.Read()
                    ),
                    transactionCancellationToken
                );

                return complianceDeclaration;
            },
            cancellationToken
        );

        complianceDeclarationMetrics.Created();
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
        var objectId = ObjectId.Parse(id);
        using var session = await dbContext.StartSession(cancellationToken);

        var deleted = await ExecuteTransaction(
            session,
            "delete",
            objectId,
            async (transactionSession, transactionCancellationToken) =>
            {
                var current = await dbContext
                    .ComplianceDeclarations.Find(
                        transactionSession,
                        Builders<ComplianceDeclaration>.Filter.Eq(x => x.Id, objectId)
                    )
                    .FirstOrDefaultAsync(cancellationToken: transactionCancellationToken);

                if (current is null)
                    return false;

                var deleteFilter = Builders<ComplianceDeclaration>.Filter.And(
                    Builders<ComplianceDeclaration>.Filter.Eq(x => x.Id, objectId),
                    Builders<ComplianceDeclaration>.Filter.Eq(x => x.Version, current.Version)
                );

                var deleteResult = await dbContext.ComplianceDeclarations.DeleteOneAsync(
                    transactionSession,
                    deleteFilter,
                    null,
                    transactionCancellationToken
                );

                if (deleteResult.DeletedCount == 0)
                    throw new ConcurrencyException(
                        $"Concurrency issue on delete, compliance declaration with id '{current.Id}' was not deleted"
                    );

                var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
                await auditEventService.RecordEvent(
                    transactionSession,
                    new AuditEventRequest(
                        Actor,
                        ComplianceDeclarationEntity,
                        AuditEventOperation.Delete,
                        "submission.removed",
                        "elevated system allowed removal",
                        current.Id.ToString(),
                        current.Version + 1,
                        current.ToBsonDocument(),
                        null,
                        current.SchemaVersion,
                        utcNow,
                        traceIdReader.Read()
                    ),
                    transactionCancellationToken
                );

                return true;
            },
            cancellationToken
        );

        if (!deleted)
            return false;

        complianceDeclarationMetrics.Deleted();
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

        var filter = Builders<ComplianceDeclaration>.Filter.And(
            Builders<ComplianceDeclaration>.Filter.Eq(x => x.Id, current.Id),
            Builders<ComplianceDeclaration>.Filter.Eq(x => x.Version, current.Version)
        );

        updated = updated with { Version = current.Version + 1, Updated = timeProvider.GetUtcNowWithoutMicroseconds() };

        await ExecuteTransaction(
            session,
            "update",
            updated.Id,
            async (transactionSession, transactionCancellationToken) =>
            {
                var replaceOneResult = await dbContext.ComplianceDeclarations.ReplaceOneAsync(
                    transactionSession,
                    filter,
                    updated,
                    new ReplaceOptions { IsUpsert = false },
                    cancellationToken: transactionCancellationToken
                );

                if (replaceOneResult.ModifiedCount == 0)
                    throw new ConcurrencyException(
                        $"Concurrency issue on write, compliance declaration with id '{current.Id}' was not updated"
                    );

                await auditEventService.RecordEvent(
                    transactionSession,
                    new AuditEventRequest(
                        Actor,
                        ComplianceDeclarationEntity,
                        AuditEventOperation.Update,
                        "submission.amended",
                        null,
                        updated.Id.ToString(),
                        updated.Version,
                        current.ToBsonDocument(),
                        updated.ToBsonDocument(),
                        updated.SchemaVersion,
                        updated.Updated,
                        traceIdReader.Read()
                    ),
                    transactionCancellationToken
                );

                return updated;
            },
            cancellationToken
        );

        complianceDeclarationMetrics.Updated(updated.Status);
        logger.LogInformation("Updated compliance declaration with id '{ComplianceDeclarationId}'", updated.Id);

        return updated;
    }

    private async Task<TResult> ExecuteTransaction<TResult>(
        IClientSessionHandle session,
        string operation,
        ObjectId complianceDeclarationId,
        Func<IClientSessionHandle, CancellationToken, Task<TResult>> callback,
        CancellationToken cancellationToken
    )
    {
        using var timeoutCancellationTokenSource = new CancellationTokenSource(_transactionTimeout);

        // Keep the driver token tied to the caller so WithTransactionAsync can abort with a live token when this
        // service-owned budget expires. Passing the budget token to the driver would also cancel its abort command.
        return await session.WithTransactionAsync(
            async (transactionSession, transactionCancellationToken) =>
            {
                using var operationCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    transactionCancellationToken,
                    timeoutCancellationTokenSource.Token
                );

                try
                {
                    return await callback(transactionSession, operationCancellationTokenSource.Token);
                }
                catch (OperationCanceledException exception)
                    when (timeoutCancellationTokenSource.IsCancellationRequested
                        && !cancellationToken.IsCancellationRequested
                    )
                {
                    logger.LogWarning(
                        exception,
                        "Compliance declaration {Operation} transaction for id '{ComplianceDeclarationId}' timed out after {TransactionTimeoutSeconds} seconds",
                        operation,
                        complianceDeclarationId,
                        _transactionTimeout.TotalSeconds
                    );

                    throw new TimeoutException(
                        $"Compliance declaration {operation} transaction timed out after {_transactionTimeout.TotalSeconds} seconds",
                        exception
                    );
                }
            },
            new TransactionOptions(maxCommitTime: _transactionTimeout),
            cancellationToken
        );
    }
}
