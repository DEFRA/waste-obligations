using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.AccountBackend;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Services.OrganisationEligibility;

public class OrganisationReferenceResolver(
    IOrganisationReferenceSearchService organisationReferenceSearchService,
    IOptions<OrganisationEligibilityOptions> options,
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
            return [];

        var sources = CreateSources(sourceRows);
        var activeRowsByKey = activeRows
            .GroupBy(x => new ReferenceKey(x.OrganisationId, x.RegistrationType))
            .ToDictionary(x => x.Key, x => x.ToArray());
        var resolutions = new Dictionary<ReferenceKey, ReferenceResolution>();
        var directProducers = new List<Source>();
        var complianceSchemes = new List<Source>();

        foreach (var source in sources)
        {
            if (ResolvedReference(activeRowsByKey.GetValueOrDefault(source.Key)) is { } referenceNumber)
            {
                WarnOnChangedResolvedSchemeLookupKey(source, activeRowsByKey[source.Key]);
                resolutions[source.Key] = new ReferenceResolution(
                    referenceNumber,
                    OrganisationReferenceNumberResolutionState.Resolved
                );
                continue;
            }

            if (source.InitialResolutionState == OrganisationReferenceNumberResolutionState.AwaitingLookupKey)
            {
                resolutions[source.Key] = new ReferenceResolution(
                    null,
                    OrganisationReferenceNumberResolutionState.AwaitingLookupKey
                );
                continue;
            }

            if (source.Key.RegistrationType == RegistrationType.DirectProducer)
                directProducers.Add(source);
            else
                complianceSchemes.Add(source);
        }

        await ResolveDirectProducers(directProducers, resolutions, cancellationToken);
        await ResolveComplianceSchemes(complianceSchemes, resolutions, cancellationToken);

        return sourceRows
            .Select(row =>
            {
                var resolution = resolutions[new ReferenceKey(row.OrganisationId, row.RegistrationType)];

                return row with
                {
                    ReferenceNumber = resolution.ReferenceNumber,
                    ReferenceNumberResolutionState = resolution.State,
                };
            })
            .OrderBy(x => x.OrganisationId)
            .ThenBy(x => x.ObligationYear)
            .ThenBy(x => x.RegistrationType)
            .ToArray();
    }

    private async Task ResolveDirectProducers(
        IReadOnlyCollection<Source> sources,
        IDictionary<ReferenceKey, ReferenceResolution> resolutions,
        CancellationToken cancellationToken
    )
    {
        foreach (var batch in sources.Chunk(options.Value.AccountReferenceNumberBatchSize))
        {
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
                    resolutions[key] = Resolve(matches);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
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
                    resolutions[source.Key] = Resolve(matches);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Account reference lookup failed for {OrganisationCount} compliance schemes",
                    batch.Length
                );
                foreach (var source in batch)
                {
                    resolutions[source.Key] = new ReferenceResolution(
                        null,
                        OrganisationReferenceNumberResolutionState.Failed
                    );
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

    private static ReferenceResolution Resolve(AccountOrganisation[] matches) =>
        matches.Length switch
        {
            0 => new ReferenceResolution(null, OrganisationReferenceNumberResolutionState.NotFound),
            1 => new ReferenceResolution(
                matches.Single().ReferenceNumber,
                OrganisationReferenceNumberResolutionState.Resolved
            ),
            _ => new ReferenceResolution(null, OrganisationReferenceNumberResolutionState.Ambiguous),
        };

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

    private readonly record struct ReferenceKey(Guid OrganisationId, RegistrationType RegistrationType);

    private sealed record Source(
        ReferenceKey Key,
        string? CompaniesHouseNumber,
        OrganisationReferenceNumberResolutionState InitialResolutionState
    );

    private sealed record ReferenceResolution(
        string? ReferenceNumber,
        OrganisationReferenceNumberResolutionState State
    );
}
