using System.Diagnostics;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.AccountBackend;
using Defra.WasteObligations.Api.Utils.Metrics;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Services.OrganisationEligibility;

public class OrganisationReferenceResolver(
    IOrganisationReferenceSearchService organisationReferenceSearchService,
    IOptions<OrganisationEligibilityOptions> options,
    IOrganisationEligibilityRefreshMetrics metrics,
    ILogger<OrganisationReferenceResolver> logger
)
{
    public async Task<IReadOnlyList<OrganisationComplianceDeclarationEligibility>> Resolve(
        IReadOnlyCollection<OrganisationComplianceDeclarationEligibility> sourceRows,
        IReadOnlyCollection<OrganisationComplianceDeclarationEligibility> activeRows,
        CancellationToken cancellationToken
    )
    {
        if (sourceRows.Count == 0)
        {
            metrics.ReferenceResolutionObserved([]);

            return [];
        }

        var sources = CreateSources(sourceRows);
        var activeRowsByKey = activeRows
            .GroupBy(x => new ReferenceKey(x.OrganisationId, x.RegistrationType))
            .ToDictionary(x => x.Key, x => x.ToArray());
        var resolutions = new Dictionary<ReferenceKey, ReferenceResolution>();
        var directProducers = new List<Source>();
        var complianceSchemes = new List<Source>();

        foreach (var source in sources)
            ProcessSource(source, activeRowsByKey, directProducers, complianceSchemes, resolutions);

        await ResolveDirectProducers(directProducers, resolutions, cancellationToken);
        await ResolveComplianceSchemes(complianceSchemes, resolutions, cancellationToken);

        var resolvedRows = sourceRows
            .Select(row =>
            {
                var resolution = resolutions[new ReferenceKey(row.OrganisationId, row.RegistrationType)];

                return row with
                {
                    ReferenceNumber = resolution.ReferenceNumber,
                    ReferenceNumberResolutionState = resolution.State,
                    SchemeOperatorName = resolution.SchemeOperatorName,
                    SourceFingerprint =
                        row.RegistrationType == RegistrationType.ComplianceScheme
                            ? Mappers.AppendAccountData(row.SourceFingerprint, resolution.SchemeOperatorName)
                            : row.SourceFingerprint,
                };
            })
            .OrderBy(x => x.OrganisationId)
            .ThenBy(x => x.ObligationYear)
            .ThenBy(x => x.RegistrationType)
            .ToArray();

        metrics.ReferenceResolutionObserved(resolvedRows);

        return resolvedRows;
    }

    private void ProcessSource(
        Source source,
        Dictionary<ReferenceKey, OrganisationComplianceDeclarationEligibility[]> activeRowsByKey,
        List<Source> directProducers,
        List<Source> complianceSchemes,
        IDictionary<ReferenceKey, ReferenceResolution> resolutions
    )
    {
        if (source.InitialResolutionState == OrganisationReferenceNumberResolutionState.AwaitingLookupKey)
        {
            var awaitingSource = source with
            {
                ExistingReferenceNumber = ResolvedReference(activeRowsByKey.GetValueOrDefault(source.Key)),
                LastKnownSchemeOperatorName = GetLastKnownSchemeOperatorName(activeRowsByKey, source.Key),
            };
            resolutions[source.Key] = FallbackResolution(
                awaitingSource,
                OrganisationReferenceNumberResolutionState.AwaitingLookupKey
            );
            return;
        }

        if (source.Key.RegistrationType == RegistrationType.DirectProducer)
        {
            if (ResolvedReference(activeRowsByKey.GetValueOrDefault(source.Key)) is { } referenceNumber)
            {
                resolutions[source.Key] = new ReferenceResolution(
                    referenceNumber,
                    OrganisationReferenceNumberResolutionState.Resolved
                );
                return;
            }

            directProducers.Add(source);
        }
        else
        {
            var existingRef = ResolvedReference(activeRowsByKey.GetValueOrDefault(source.Key));
            var lastKnownName = GetLastKnownSchemeOperatorName(activeRowsByKey, source.Key);

            if (existingRef != null && lastKnownName != null)
            {
                resolutions[source.Key] = new ReferenceResolution(
                    existingRef,
                    OrganisationReferenceNumberResolutionState.Resolved,
                    lastKnownName
                );
                return;
            }

            if (existingRef != null)
                WarnOnChangedResolvedSchemeLookupKey(source, activeRowsByKey[source.Key]);

            complianceSchemes.Add(
                source with
                {
                    ExistingReferenceNumber = existingRef,
                    LastKnownSchemeOperatorName = lastKnownName,
                }
            );
        }
    }

    private async Task ResolveDirectProducers(
        IReadOnlyCollection<Source> sources,
        IDictionary<ReferenceKey, ReferenceResolution> resolutions,
        CancellationToken cancellationToken
    )
    {
        foreach (var batch in sources.Chunk(options.Value.AccountReferenceNumberBatchSize))
        {
            var lookupStopwatch = Stopwatch.StartNew();

            try
            {
                var response = await organisationReferenceSearchService.SearchOrganisationsByExternalIds(
                    batch.Select(x => x.Key.OrganisationId).ToArray(),
                    cancellationToken
                );
                foreach (var key in batch.Select(x => x.Key))
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
                    resolutions[key] = matches.Length switch
                    {
                        0 => new ReferenceResolution(null, OrganisationReferenceNumberResolutionState.NotFound),
                        1 => new ReferenceResolution(
                            matches.Single().ReferenceNumber,
                            OrganisationReferenceNumberResolutionState.Resolved
                        ),
                        _ => new ReferenceResolution(null, OrganisationReferenceNumberResolutionState.Ambiguous),
                    };
                }

                lookupStopwatch.Stop();
                metrics.AccountReferenceLookupCompleted(
                    RegistrationType.DirectProducer,
                    batch.Length,
                    lookupStopwatch.Elapsed
                );
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                lookupStopwatch.Stop();
                metrics.AccountReferenceLookupFailed(RegistrationType.DirectProducer, lookupStopwatch.Elapsed);
                logger.LogWarning(
                    exception,
                    "Account reference lookup failed for {OrganisationCount} direct producers",
                    batch.Length
                );
                foreach (var key in batch.Select(x => x.Key))
                {
                    resolutions[key] = new ReferenceResolution(null, OrganisationReferenceNumberResolutionState.Failed);
                }
            }
        }
    }

    private async Task ResolveComplianceSchemes(
        IReadOnlyCollection<Source> sources,
        IDictionary<ReferenceKey, ReferenceResolution> resolutions,
        CancellationToken cancellationToken
    )
    {
        foreach (var batch in sources.Chunk(options.Value.AccountReferenceNumberBatchSize))
        {
            var companiesHouseNumbers = batch.Select(x => x.CompaniesHouseNumber!).Distinct().ToArray();
            var lookupStopwatch = Stopwatch.StartNew();

            try
            {
                var response = await organisationReferenceSearchService.SearchOrganisationsByCompaniesHouseNumbers(
                    companiesHouseNumbers,
                    cancellationToken
                );
                foreach (var source in batch)
                {
                    var matches = response
                        .Where(x =>
                            x.IsComplianceScheme
                            && string.Equals(
                                x.CompaniesHouseNumber,
                                source.CompaniesHouseNumber,
                                StringComparison.OrdinalIgnoreCase
                            )
                            && !string.IsNullOrWhiteSpace(x.ReferenceNumber)
                        )
                        .ToArray();
                    resolutions[source.Key] = ResolveWithName(source, matches);
                }

                lookupStopwatch.Stop();
                metrics.AccountReferenceLookupCompleted(
                    RegistrationType.ComplianceScheme,
                    companiesHouseNumbers.Length,
                    lookupStopwatch.Elapsed
                );
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                lookupStopwatch.Stop();
                metrics.AccountReferenceLookupFailed(RegistrationType.ComplianceScheme, lookupStopwatch.Elapsed);
                logger.LogWarning(
                    exception,
                    "Account reference lookup failed for {OrganisationCount} compliance schemes",
                    batch.Length
                );
                foreach (var source in batch)
                {
                    resolutions[source.Key] = FallbackResolution(source);
                }
            }
        }
    }

    private void WarnOnChangedResolvedSchemeLookupKey(
        Source source,
        IReadOnlyCollection<OrganisationComplianceDeclarationEligibility> activeRows
    )
    {
        if (source.Key.RegistrationType != RegistrationType.ComplianceScheme)
            return;

        var activeCompaniesHouseNumbers = activeRows
            .Select(x => x.CompaniesHouseNumber)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (
            activeCompaniesHouseNumbers.Length == 1
            && string.Equals(
                activeCompaniesHouseNumbers.Single(),
                source.CompaniesHouseNumber,
                StringComparison.OrdinalIgnoreCase
            )
        )
            return;

        logger.LogError(
            "Organisation reference lookup key changed after resolution for organisation {OrganisationId} and registration type {RegistrationType}. The existing reference number will be retained",
            source.Key.OrganisationId,
            source.Key.RegistrationType
        );
    }

    private static ReferenceResolution ResolveWithName(Source source, AccountOrganisation[] matches)
    {
        if (matches.Length == 1)
            return new ReferenceResolution(
                source.ExistingReferenceNumber ?? matches.Single().ReferenceNumber,
                OrganisationReferenceNumberResolutionState.Resolved,
                matches.Single().Name
            );

        var stateIfUnresolved =
            matches.Length > 1
                ? OrganisationReferenceNumberResolutionState.Ambiguous
                : OrganisationReferenceNumberResolutionState.NotFound;
        return FallbackResolution(source, stateIfUnresolved);
    }

    private static ReferenceResolution FallbackResolution(
        Source source,
        OrganisationReferenceNumberResolutionState? stateIfUnresolved = null
    ) =>
        source.ExistingReferenceNumber != null
            ? new ReferenceResolution(
                source.ExistingReferenceNumber,
                OrganisationReferenceNumberResolutionState.Resolved,
                source.LastKnownSchemeOperatorName
            )
            : new ReferenceResolution(null, stateIfUnresolved ?? OrganisationReferenceNumberResolutionState.Failed);

    private static string? ResolvedReference(
        IReadOnlyCollection<OrganisationComplianceDeclarationEligibility>? activeRows
    )
    {
        var referenceNumbers = activeRows
            ?.Where(x =>
                x.ReferenceNumberResolutionState == OrganisationReferenceNumberResolutionState.Resolved
                && !string.IsNullOrWhiteSpace(x.ReferenceNumber)
            )
            .Select(x => x.ReferenceNumber!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (referenceNumbers is null || referenceNumbers.Length == 0)
            return null;
        if (referenceNumbers.Length > 1)
        {
            throw new InvalidOperationException("Active eligibility rows have conflicting resolved reference numbers");
        }

        return referenceNumbers.Single();
    }

    private static Source[] CreateSources(
        IReadOnlyCollection<OrganisationComplianceDeclarationEligibility> sourceRows
    ) =>
        sourceRows
            .GroupBy(x => new ReferenceKey(x.OrganisationId, x.RegistrationType))
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
                var initialResolutionState =
                    group.Key.RegistrationType == RegistrationType.ComplianceScheme
                    && string.IsNullOrWhiteSpace(companiesHouseNumber)
                        ? OrganisationReferenceNumberResolutionState.AwaitingLookupKey
                        : OrganisationReferenceNumberResolutionState.Pending;

                return new Source(group.Key, companiesHouseNumber, initialResolutionState);
            })
            .OrderBy(x => x.Key.OrganisationId)
            .ThenBy(x => x.Key.RegistrationType)
            .ToArray();

    private static string? GetLastKnownSchemeOperatorName(
        Dictionary<ReferenceKey, OrganisationComplianceDeclarationEligibility[]> activeRowsByKey,
        ReferenceKey key
    ) => activeRowsByKey.GetValueOrDefault(key)?.Select(x => x.SchemeOperatorName).FirstOrDefault(name => name != null);

    private readonly record struct ReferenceKey(Guid OrganisationId, RegistrationType RegistrationType);

    private sealed record Source(
        ReferenceKey Key,
        string? CompaniesHouseNumber,
        OrganisationReferenceNumberResolutionState InitialResolutionState,
        string? ExistingReferenceNumber = null,
        string? LastKnownSchemeOperatorName = null
    );

    private sealed record ReferenceResolution(
        string? ReferenceNumber,
        OrganisationReferenceNumberResolutionState State,
        string? SchemeOperatorName = null
    );
}
