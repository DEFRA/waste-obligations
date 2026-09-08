using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services.OrganisationObligations;

public class OrganisationObligationHistoricalBackfillStore(IMongoDatabase database, TimeProvider timeProvider)
    : IOrganisationObligationHistoricalBackfillStore
{
    private const int DuplicateKeyErrorCode = 11000;

    private readonly IMongoCollection<OrganisationObligationHistoricalBackfill> _backfills =
        database.GetCollection<OrganisationObligationHistoricalBackfill>(
            OrganisationObligationHistoricalBackfill.CollectionName
        );

    public async Task<OrganisationObligationHistoricalBackfillCreation> Create(
        OrganisationObligationHistoricalBackfill backfill,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var existing = await _backfills.FindOneAndUpdateAsync(
                x => x.ObligationYear == backfill.ObligationYear,
                Builders<OrganisationObligationHistoricalBackfill>
                    .Update.SetOnInsert(x => x.ObligationYear, backfill.ObligationYear)
                    .SetOnInsert(x => x.Targets, backfill.Targets)
                    .SetOnInsert(x => x.RequestedAt, backfill.RequestedAt)
                    .SetOnInsert(x => x.UpdatedAt, backfill.UpdatedAt),
                new FindOneAndUpdateOptions<OrganisationObligationHistoricalBackfill>
                {
                    IsUpsert = true,
                    ReturnDocument = ReturnDocument.Before,
                },
                cancellationToken
            );

            return new OrganisationObligationHistoricalBackfillCreation
            {
                Backfill = existing ?? backfill,
                WasCreated = existing is null,
            };
        }
        catch (MongoCommandException exception) when (exception.Code == DuplicateKeyErrorCode)
        {
            return await ExistingBackfill(backfill.ObligationYear, cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Code == DuplicateKeyErrorCode)
        {
            return await ExistingBackfill(backfill.ObligationYear, cancellationToken);
        }
    }

    public async Task<OrganisationObligationHistoricalBackfill?> GetNextIncomplete(CancellationToken cancellationToken)
    {
        var backfills = await _backfills
            .Find(x => x.CompletedAt == null)
            .SortBy(x => x.RequestedAt)
            .Limit(1)
            .ToListAsync(cancellationToken);

        return backfills.SingleOrDefault();
    }

    public async Task<OrganisationObligationHistoricalBackfill[]> GetAll(CancellationToken cancellationToken)
    {
        var backfills = await _backfills
            .Find(FilterDefinition<OrganisationObligationHistoricalBackfill>.Empty)
            .SortBy(x => x.ObligationYear)
            .ToListAsync(cancellationToken);

        return backfills.ToArray();
    }

    public async Task MarkEnqueued(
        OrganisationObligationHistoricalBackfill backfill,
        CancellationToken cancellationToken
    )
    {
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        await _backfills.UpdateOneAsync(
            x =>
                x.ObligationYear == backfill.ObligationYear
                && x.RequestedAt == backfill.RequestedAt
                && x.EnqueuedAt == null,
            Builders<OrganisationObligationHistoricalBackfill>
                .Update.Set(x => x.EnqueuedAt, utcNow)
                .Set(x => x.UpdatedAt, utcNow),
            cancellationToken: cancellationToken
        );
    }

    public async Task MarkDeferred(
        OrganisationObligationHistoricalBackfill backfill,
        OrganisationObligationHistoricalBackfillDeferralReason reason,
        CancellationToken cancellationToken
    )
    {
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        await _backfills.UpdateOneAsync(
            x => x.ObligationYear == backfill.ObligationYear && x.RequestedAt == backfill.RequestedAt,
            Builders<OrganisationObligationHistoricalBackfill>
                .Update.Set(x => x.DeferralReason, reason)
                .Set(x => x.DeferredAt, utcNow)
                .Set(x => x.UpdatedAt, utcNow),
            cancellationToken: cancellationToken
        );
    }

    public async Task ClearDeferral(
        OrganisationObligationHistoricalBackfill backfill,
        CancellationToken cancellationToken
    )
    {
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        await _backfills.UpdateOneAsync(
            x =>
                x.ObligationYear == backfill.ObligationYear
                && x.RequestedAt == backfill.RequestedAt
                && x.DeferralReason != null,
            Builders<OrganisationObligationHistoricalBackfill>
                .Update.Unset(x => x.DeferralReason)
                .Unset(x => x.DeferredAt)
                .Set(x => x.UpdatedAt, utcNow),
            cancellationToken: cancellationToken
        );
    }

    public async Task Complete(OrganisationObligationHistoricalBackfill backfill, CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        await _backfills.UpdateOneAsync(
            x =>
                x.ObligationYear == backfill.ObligationYear
                && x.RequestedAt == backfill.RequestedAt
                && x.CompletedAt == null,
            Builders<OrganisationObligationHistoricalBackfill>
                .Update.Set(x => x.CompletedAt, utcNow)
                .Set(x => x.UpdatedAt, utcNow),
            cancellationToken: cancellationToken
        );
    }

    private async Task<OrganisationObligationHistoricalBackfillCreation> ExistingBackfill(
        int obligationYear,
        CancellationToken cancellationToken
    )
    {
        var existing = await _backfills.Find(x => x.ObligationYear == obligationYear).SingleAsync(cancellationToken);

        return new OrganisationObligationHistoricalBackfillCreation { Backfill = existing, WasCreated = false };
    }
}
