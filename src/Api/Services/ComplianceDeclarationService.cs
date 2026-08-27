using System.Text.RegularExpressions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Utils.Logging;
using Defra.WasteObligations.Api.Utils.Metrics;
using Defra.WasteObligations.AuditEvents;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services;

public class ComplianceDeclarationService(
    IDbContext dbContext,
    ILogger<ComplianceDeclarationService> logger,
    TimeProvider timeProvider,
    IAuditEventService auditEventService,
    IComplianceDeclarationMetrics complianceDeclarationMetrics,
    TraceIdReader traceIdReader,
    IComplianceDeclarationReviewStateService complianceDeclarationReviewStateService
) : IComplianceDeclarationService
{
    private const string Actor = "service:waste-obligations";
    private const string ComplianceDeclarationEntity = "compliance_declaration";

    public async Task<ComplianceDeclaration> Create(
        ComplianceDeclaration complianceDeclaration,
        CancellationToken cancellationToken
    )
    {
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        complianceDeclaration = complianceDeclaration with { Version = 1, Created = utcNow, Updated = utcNow };

        await dbContext.ExecuteTransaction(
            async (transactionSession, transactionCancellationToken) =>
            {
                await dbContext.ComplianceDeclarations.InsertOneAsync(
                    transactionSession,
                    complianceDeclaration,
                    cancellationToken: transactionCancellationToken
                );

                await complianceDeclarationReviewStateService.Refresh(
                    transactionSession,
                    [complianceDeclaration],
                    utcNow,
                    transactionCancellationToken
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
            $"compliance declaration create {complianceDeclaration.Id}",
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

    public async Task<ComplianceDeclarationPageResult> Read(
        Guid organisationId,
        int obligationYear,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        var filter = Builders<ComplianceDeclaration>.Filter.And(
            Builders<ComplianceDeclaration>.Filter.Eq(x => x.Organisation.Id, organisationId),
            Builders<ComplianceDeclaration>.Filter.Eq(x => x.ObligationYear, obligationYear)
        );

        return await ReadPaged(
            filter,
            Builders<ComplianceDeclaration>.Sort.Descending(x => x.Updated).Ascending(x => x.Id),
            page,
            pageSize,
            cancellationToken
        );
    }

    public async Task<bool> Delete(string id, CancellationToken cancellationToken)
    {
        var objectId = ObjectId.Parse(id);
        var deleted = await dbContext.ExecuteTransaction(
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

                await complianceDeclarationReviewStateService.Refresh(
                    transactionSession,
                    [current],
                    timeProvider.GetUtcNowWithoutMicroseconds(),
                    transactionCancellationToken
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
            $"compliance declaration delete {objectId}",
            cancellationToken
        );

        if (!deleted)
            return false;

        complianceDeclarationMetrics.Deleted();
        logger.LogInformation("Deleted compliance declaration with id '{ComplianceDeclarationId}'", id);

        return true;
    }

    public async Task<ComplianceDeclarationPageResult> Search(
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

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Every name field is matched because which one the regulator sees depends on
            // the organisation type: compliance scheme declarations leave Name null and are
            // displayed by their scheme operator name.
            var pattern = new BsonRegularExpression(Regex.Escape(query.Search.Trim()), "i");

            filters.Add(
                Builders<ComplianceDeclaration>.Filter.Or(
                    Builders<ComplianceDeclaration>.Filter.Regex(x => x.Organisation.Name, pattern),
                    Builders<ComplianceDeclaration>.Filter.Regex(x => x.Organisation.ComplianceSchemeName, pattern),
                    Builders<ComplianceDeclaration>.Filter.Regex(x => x.Organisation.SchemeOperatorName, pattern),
                    Builders<ComplianceDeclaration>.Filter.Regex(x => x.Organisation.ReferenceNumber, pattern)
                )
            );
        }

        var combinedFilter =
            filters.Count == 0
                ? Builders<ComplianceDeclaration>.Filter.Empty
                : Builders<ComplianceDeclaration>.Filter.And(filters);

        return await ReadPaged(combinedFilter, BuildSearchSort(query.Sort), page, pageSize, cancellationToken);
    }

    public async Task<ComplianceDeclaration> Update(
        ComplianceDeclaration current,
        ComplianceDeclaration updated,
        CancellationToken cancellationToken
    )
    {
        var filter = Builders<ComplianceDeclaration>.Filter.And(
            Builders<ComplianceDeclaration>.Filter.Eq(x => x.Id, current.Id),
            Builders<ComplianceDeclaration>.Filter.Eq(x => x.Version, current.Version)
        );

        updated = updated with { Version = current.Version + 1, Updated = timeProvider.GetUtcNowWithoutMicroseconds() };

        await dbContext.ExecuteTransaction(
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

                await complianceDeclarationReviewStateService.Refresh(
                    transactionSession,
                    [current, updated],
                    updated.Updated,
                    transactionCancellationToken
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
            $"compliance declaration update {updated.Id}",
            cancellationToken
        );

        complianceDeclarationMetrics.Updated(updated.Status);
        logger.LogInformation("Updated compliance declaration with id '{ComplianceDeclarationId}'", updated.Id);

        return updated;
    }

    public Task<ComplianceDeclaration> UpdateStatus(
        ComplianceDeclaration current,
        ComplianceDeclarationStatus status,
        string? reason,
        User user,
        CancellationToken cancellationToken
    )
    {
        var updated = current.UpdateStatus(status, reason, user, timeProvider.GetUtcNowWithoutMicroseconds());

        return Update(current, updated, cancellationToken);
    }

    private async Task<ComplianceDeclarationPageResult> ReadPaged(
        FilterDefinition<ComplianceDeclaration> filter,
        SortDefinition<ComplianceDeclaration> sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        var countTask = dbContext.ComplianceDeclarations.CountDocumentsAsync(
            filter,
            cancellationToken: cancellationToken
        );
        var resultsTask = dbContext
            .ComplianceDeclarations.Find(filter)
            .Sort(sort)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        await Task.WhenAll(countTask, resultsTask);

        return new ComplianceDeclarationPageResult
        {
            ComplianceDeclarations = resultsTask.Result,
            Total = (int)countTask.Result,
        };
    }

    private static SortDefinition<ComplianceDeclaration> BuildSearchSort(
        IReadOnlyCollection<ComplianceDeclarationSort>? sort
    )
    {
        var sortBuilder = Builders<ComplianceDeclaration>.Sort;
        if (sort is not { Count: > 0 })
            return sortBuilder.Ascending(x => x.Id);

        var sortDefinitions = sort.Select(BuildSearchSort).ToList();
        if (!sort.Any(x => x.Field is ComplianceDeclarationSortField.OrganisationName))
            sortDefinitions.Add(sortBuilder.Ascending(x => x.Organisation.Name));

        sortDefinitions.Add(sortBuilder.Ascending(x => x.Id));

        return sortBuilder.Combine(sortDefinitions);
    }

    private static SortDefinition<ComplianceDeclaration> BuildSearchSort(ComplianceDeclarationSort sort) =>
        sort.Field switch
        {
            ComplianceDeclarationSortField.RecyclingObligations => SortByReversedDirection(
                sort.Direction,
                Builders<ComplianceDeclaration>.Sort.Ascending(x => x.ObligationStatus),
                Builders<ComplianceDeclaration>.Sort.Descending(x => x.ObligationStatus)
            ),
            ComplianceDeclarationSortField.PercentageMet => SortByDirection(
                sort.Direction,
                Builders<ComplianceDeclaration>.Sort.Ascending(x => x.ObligationCoveragePercentage),
                Builders<ComplianceDeclaration>.Sort.Descending(x => x.ObligationCoveragePercentage)
            ),
            ComplianceDeclarationSortField.DateSubmitted => SortByDirection(
                sort.Direction,
                Builders<ComplianceDeclaration>.Sort.Ascending(x => x.Created),
                Builders<ComplianceDeclaration>.Sort.Descending(x => x.Created)
            ),
            ComplianceDeclarationSortField.Regulation43 => SortByDirection(
                sort.Direction,
                Builders<ComplianceDeclaration>.Sort.Ascending(x => x.IsRegulation43Compliant),
                Builders<ComplianceDeclaration>.Sort.Descending(x => x.IsRegulation43Compliant)
            ),
            ComplianceDeclarationSortField.OrganisationName => SortByDirection(
                sort.Direction,
                Builders<ComplianceDeclaration>.Sort.Ascending(x => x.Organisation.Name),
                Builders<ComplianceDeclaration>.Sort.Descending(x => x.Organisation.Name)
            ),
            ComplianceDeclarationSortField.OrganisationId => SortByDirection(
                sort.Direction,
                Builders<ComplianceDeclaration>.Sort.Ascending(x => x.Organisation.Id),
                Builders<ComplianceDeclaration>.Sort.Descending(x => x.Organisation.Id)
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(sort)),
        };

    private static SortDefinition<ComplianceDeclaration> SortByDirection(
        ComplianceDeclarationSortDirection direction,
        SortDefinition<ComplianceDeclaration> ascending,
        SortDefinition<ComplianceDeclaration> descending
    ) => direction is ComplianceDeclarationSortDirection.Ascending ? ascending : descending;

    private static SortDefinition<ComplianceDeclaration> SortByReversedDirection(
        ComplianceDeclarationSortDirection direction,
        SortDefinition<ComplianceDeclaration> ascending,
        SortDefinition<ComplianceDeclaration> descending
    ) => direction is ComplianceDeclarationSortDirection.Ascending ? descending : ascending;
}
