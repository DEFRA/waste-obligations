using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.AccountBackend;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services.OrganisationEligibility;

public class OrganisationReferenceCacheService(
    IDbContext dbContext,
    IOrganisationReferenceSearchService organisationReferenceSearchService,
    IOptions<OrganisationEligibilityOptions> options,
    TimeProvider timeProvider
)
{
    public async Task<IReadOnlyList<OrganisationReferenceCache>> SynchroniseAndResolve(
        IReadOnlyCollection<Data.Entities.OrganisationComplianceDeclarationEligibility> eligibilityRows,
        CancellationToken cancellationToken
    )
    {
        if (eligibilityRows.Count == 0)
            return [];

        var sources = CreateSources(eligibilityRows);
        var organisationIds = sources.Select(x => x.Key.OrganisationId).Distinct().ToArray();
        var existingCaches = await dbContext
            .OrganisationReferenceCaches.Find(
                Builders<OrganisationReferenceCache>.Filter.In(x => x.OrganisationId, organisationIds)
            )
            .ToListAsync(cancellationToken);
        var existingCachesByKey = existingCaches.ToDictionary(x => new CacheKey(x.OrganisationId, x.RegistrationType));
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var cachesByKey = new Dictionary<CacheKey, OrganisationReferenceCache>();
        var changedKeys = new HashSet<CacheKey>();

        foreach (var source in sources)
        {
            var cache = Synchronise(source, existingCachesByKey.GetValueOrDefault(source.Key), utcNow);
            cachesByKey.Add(source.Key, cache.Cache);

            if (cache.Changed)
                changedKeys.Add(source.Key);
        }

        var dueCaches = cachesByKey.Where(x => IsDue(x.Value, utcNow)).Select(x => x.Key).ToArray();

        await ResolveDirectProducers(dueCaches, cachesByKey, changedKeys, utcNow, cancellationToken);
        await ResolveComplianceSchemes(dueCaches, cachesByKey, changedKeys, utcNow, cancellationToken);

        if (changedKeys.Count > 0)
        {
            var writes = changedKeys.Select(key => new ReplaceOneModel<OrganisationReferenceCache>(
                Builders<OrganisationReferenceCache>.Filter.And(
                    Builders<OrganisationReferenceCache>.Filter.Eq(x => x.OrganisationId, key.OrganisationId),
                    Builders<OrganisationReferenceCache>.Filter.Eq(x => x.RegistrationType, key.RegistrationType)
                ),
                cachesByKey[key]
            )
            {
                IsUpsert = true,
            });

            await dbContext.OrganisationReferenceCaches.BulkWriteAsync(writes, cancellationToken: cancellationToken);
        }

        return cachesByKey.Values.OrderBy(x => x.OrganisationId).ThenBy(x => x.RegistrationType).ToArray();
    }

    private async Task ResolveDirectProducers(
        IReadOnlyCollection<CacheKey> dueCaches,
        IDictionary<CacheKey, OrganisationReferenceCache> cachesByKey,
        ISet<CacheKey> changedKeys,
        DateTime utcNow,
        CancellationToken cancellationToken
    )
    {
        var candidates = dueCaches
            .Where(x => cachesByKey[x].LookupMode == OrganisationReferenceLookupMode.AccountExternalId)
            .ToArray();

        foreach (var batch in candidates.Chunk(options.Value.AccountReferenceNumberBatchSize))
        {
            try
            {
                var response = await organisationReferenceSearchService.SearchOrganisationsByExternalIds(
                    batch.Select(x => x.OrganisationId).ToArray(),
                    cancellationToken
                );

                foreach (var key in batch)
                {
                    var matches = response
                        .Organisations.Where(x =>
                            string.Equals(
                                x.ExternalId,
                                key.OrganisationId.ToString("D"),
                                StringComparison.OrdinalIgnoreCase
                            ) && !string.IsNullOrWhiteSpace(x.ReferenceNumber)
                        )
                        .ToArray();
                    cachesByKey[key] = Resolve(cachesByKey[key], matches, utcNow);
                    changedKeys.Add(key);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                foreach (var key in batch)
                {
                    cachesByKey[key] = Fail(cachesByKey[key], exception.Message, utcNow);
                    changedKeys.Add(key);
                }
            }
        }
    }

    private async Task ResolveComplianceSchemes(
        IReadOnlyCollection<CacheKey> dueCaches,
        IDictionary<CacheKey, OrganisationReferenceCache> cachesByKey,
        ISet<CacheKey> changedKeys,
        DateTime utcNow,
        CancellationToken cancellationToken
    )
    {
        var candidates = dueCaches
            .Where(x => cachesByKey[x].LookupMode == OrganisationReferenceLookupMode.CompaniesHouseNumber)
            .ToArray();

        foreach (var batch in candidates.Chunk(options.Value.AccountReferenceNumberBatchSize))
        {
            var companiesHouseNumbers = batch.Select(x => cachesByKey[x].CompaniesHouseNumber!).Distinct().ToArray();

            try
            {
                var response = await organisationReferenceSearchService.SearchOrganisationsByCompaniesHouseNumbers(
                    companiesHouseNumbers,
                    cancellationToken
                );

                foreach (var key in batch)
                {
                    var matches = response
                        .Where(x =>
                            x.IsComplianceScheme
                            && string.Equals(
                                x.CompaniesHouseNumber,
                                cachesByKey[key].CompaniesHouseNumber,
                                StringComparison.OrdinalIgnoreCase
                            )
                            && !string.IsNullOrWhiteSpace(x.ReferenceNumber)
                        )
                        .ToArray();
                    cachesByKey[key] = Resolve(cachesByKey[key], matches, utcNow);
                    changedKeys.Add(key);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                foreach (var key in batch)
                {
                    cachesByKey[key] = Fail(cachesByKey[key], exception.Message, utcNow);
                    changedKeys.Add(key);
                }
            }
        }
    }

    private static SynchronisedCache Synchronise(
        Source source,
        OrganisationReferenceCache? existingCache,
        DateTime utcNow
    )
    {
        if (existingCache is null)
        {
            return new SynchronisedCache(
                new OrganisationReferenceCache
                {
                    OrganisationId = source.Key.OrganisationId,
                    RegistrationType = source.Key.RegistrationType,
                    LookupMode = source.LookupMode,
                    CompaniesHouseNumber = source.CompaniesHouseNumber,
                    ResolutionState = source.InitialResolutionState,
                    FirstSeenAt = utcNow,
                    LastSeenAt = utcNow,
                    NextAttemptAt =
                        source.InitialResolutionState == OrganisationReferenceNumberResolutionState.Pending
                            ? utcNow
                            : null,
                },
                true
            );
        }

        if (
            existingCache.LookupMode == source.LookupMode
            && string.Equals(
                existingCache.CompaniesHouseNumber,
                source.CompaniesHouseNumber,
                StringComparison.OrdinalIgnoreCase
            )
            && existingCache.ResolutionState != OrganisationReferenceNumberResolutionState.AwaitingLookupKey
        )
            return new SynchronisedCache(existingCache, false);

        if (existingCache.ResolutionState == OrganisationReferenceNumberResolutionState.Resolved)
        {
            return new SynchronisedCache(
                existingCache with
                {
                    LookupMode = source.LookupMode,
                    CompaniesHouseNumber = source.CompaniesHouseNumber,
                    LastSeenAt = utcNow,
                },
                true
            );
        }

        return new SynchronisedCache(
            existingCache with
            {
                LookupMode = source.LookupMode,
                CompaniesHouseNumber = source.CompaniesHouseNumber,
                ResolutionState = source.InitialResolutionState,
                LastSeenAt = utcNow,
                NextAttemptAt =
                    source.InitialResolutionState == OrganisationReferenceNumberResolutionState.Pending ? utcNow : null,
                LastFailure = null,
            },
            true
        );
    }

    private OrganisationReferenceCache Resolve(
        OrganisationReferenceCache cache,
        AccountOrganisation[] matches,
        DateTime utcNow
    ) =>
        matches.Length switch
        {
            0 => NotFound(cache, utcNow),
            1 => Resolved(cache, matches.Single(), utcNow),
            _ => Ambiguous(cache, utcNow),
        };

    private static OrganisationReferenceCache Resolved(
        OrganisationReferenceCache cache,
        AccountOrganisation accountOrganisation,
        DateTime utcNow
    ) =>
        cache with
        {
            ReferenceNumber = accountOrganisation.ReferenceNumber,
            ResolutionState = OrganisationReferenceNumberResolutionState.Resolved,
            ResolvedAccountExternalId = Guid.TryParse(accountOrganisation.ExternalId, out var externalId)
                ? externalId
                : null,
            ResolvedUsingCompaniesHouseNumber =
                cache.LookupMode == OrganisationReferenceLookupMode.CompaniesHouseNumber
                    ? cache.CompaniesHouseNumber
                    : null,
            LastAttemptedAt = utcNow,
            NextAttemptAt = null,
            AttemptCount = cache.AttemptCount + 1,
            ResolvedAt = utcNow,
            LastFailure = null,
        };

    private OrganisationReferenceCache NotFound(OrganisationReferenceCache cache, DateTime utcNow) =>
        cache with
        {
            ReferenceNumber = null,
            ResolutionState = OrganisationReferenceNumberResolutionState.NotFound,
            ResolvedAccountExternalId = null,
            ResolvedUsingCompaniesHouseNumber = null,
            LastAttemptedAt = utcNow,
            NextAttemptAt = utcNow.Add(options.Value.ReferenceNumberRetryDelay),
            AttemptCount = cache.AttemptCount + 1,
            ResolvedAt = null,
            LastFailure = null,
        };

    private static OrganisationReferenceCache Ambiguous(OrganisationReferenceCache cache, DateTime utcNow) =>
        cache with
        {
            ReferenceNumber = null,
            ResolutionState = OrganisationReferenceNumberResolutionState.Ambiguous,
            ResolvedAccountExternalId = null,
            ResolvedUsingCompaniesHouseNumber = null,
            LastAttemptedAt = utcNow,
            NextAttemptAt = null,
            AttemptCount = cache.AttemptCount + 1,
            ResolvedAt = null,
            LastFailure = "Account returned multiple matching organisations",
        };

    private OrganisationReferenceCache Fail(OrganisationReferenceCache cache, string failure, DateTime utcNow) =>
        cache with
        {
            ResolutionState = OrganisationReferenceNumberResolutionState.Failed,
            LastAttemptedAt = utcNow,
            NextAttemptAt = utcNow.Add(options.Value.ReferenceNumberRetryDelay),
            AttemptCount = cache.AttemptCount + 1,
            LastFailure = failure.Length > 500 ? failure[..500] : failure,
        };

    private static bool IsDue(OrganisationReferenceCache cache, DateTime utcNow) =>
        cache.ResolutionState
            is OrganisationReferenceNumberResolutionState.Pending
                or OrganisationReferenceNumberResolutionState.NotFound
                or OrganisationReferenceNumberResolutionState.Failed
        && cache.NextAttemptAt <= utcNow;

    private static Source[] CreateSources(
        IReadOnlyCollection<Data.Entities.OrganisationComplianceDeclarationEligibility> eligibilityRows
    ) =>
        eligibilityRows
            .GroupBy(x => new CacheKey(x.OrganisationId, x.RegistrationType))
            .Select(group =>
            {
                var companiesHouseNumbers = group
                    .Select(x => x.CompaniesHouseNumber)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (companiesHouseNumbers.Length > 1)
                {
                    throw new InvalidOperationException(
                        $"Organisation {group.Key.OrganisationId:D} has inconsistent Companies House numbers for {group.Key.RegistrationType}"
                    );
                }

                var companiesHouseNumber = companiesHouseNumbers.Single();
                var lookupMode =
                    group.Key.RegistrationType == RegistrationType.DirectProducer
                        ? OrganisationReferenceLookupMode.AccountExternalId
                        : OrganisationReferenceLookupMode.CompaniesHouseNumber;
                var initialResolutionState =
                    lookupMode == OrganisationReferenceLookupMode.CompaniesHouseNumber
                    && string.IsNullOrWhiteSpace(companiesHouseNumber)
                        ? OrganisationReferenceNumberResolutionState.AwaitingLookupKey
                        : OrganisationReferenceNumberResolutionState.Pending;

                return new Source(group.Key, lookupMode, companiesHouseNumber, initialResolutionState);
            })
            .OrderBy(x => x.Key.OrganisationId)
            .ThenBy(x => x.Key.RegistrationType)
            .ToArray();

    private readonly record struct CacheKey(Guid OrganisationId, RegistrationType RegistrationType);

    private sealed record Source(
        CacheKey Key,
        OrganisationReferenceLookupMode LookupMode,
        string? CompaniesHouseNumber,
        OrganisationReferenceNumberResolutionState InitialResolutionState
    );

    private sealed record SynchronisedCache(OrganisationReferenceCache Cache, bool Changed);
}
