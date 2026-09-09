using System.Runtime.CompilerServices;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.AuditEvents.Data;
using Defra.WasteObligations.AuditEvents.Entities;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services.Admin;

public class AdminDataService(IMongoDatabase database) : IAdminDataService
{
    private const string AuditEventCounterId = "audit_event";
    private readonly IMongoCollection<AuditEventCounter> _auditEventCounters =
        database.GetCollection<AuditEventCounter>(AuditEventDbContext.AuditEventCounterCollectionName);

    private readonly IMongoCollection<AuditEvent> _auditEvents = database.GetCollection<AuditEvent>(nameof(AuditEvent));

    private readonly IMongoCollection<ComplianceDeclaration> _complianceDeclarations =
        database.GetCollection<ComplianceDeclaration>(nameof(ComplianceDeclaration));

    private readonly IMongoCollection<OrganisationComplianceDeclarationEligibility> _eligibilities =
        database.GetCollection<OrganisationComplianceDeclarationEligibility>(
            nameof(OrganisationComplianceDeclarationEligibility)
        );

    private readonly IMongoCollection<OrganisationEligibilitySnapshot> _eligibilitySnapshots =
        database.GetCollection<OrganisationEligibilitySnapshot>(nameof(OrganisationEligibilitySnapshot));

    private readonly IMongoCollection<OrganisationObligationHistoricalBackfill> _historicalBackfills =
        database.GetCollection<OrganisationObligationHistoricalBackfill>(
            OrganisationObligationHistoricalBackfill.CollectionName
        );

    private readonly IMongoCollection<OrganisationObligationRequestPacingState> _requestPacingStates =
        database.GetCollection<OrganisationObligationRequestPacingState>(
            OrganisationObligationRequestPacingState.CollectionName
        );

    private readonly IMongoCollection<OrganisationObligationSummary> _obligationSummaries =
        database.GetCollection<OrganisationObligationSummary>(nameof(OrganisationObligationSummary));

    public IAsyncEnumerable<ComplianceDeclaration> ReadComplianceDeclarations(
        Guid organisationId,
        int? obligationYear,
        CancellationToken cancellationToken
    )
    {
        var filter = Builders<ComplianceDeclaration>.Filter.Eq(x => x.Organisation.Id, organisationId);
        if (obligationYear is not null)
        {
            filter &= Builders<ComplianceDeclaration>.Filter.Eq(x => x.ObligationYear, obligationYear.Value);
        }

        var query = _complianceDeclarations
            .Find(filter)
            .SortBy(x => x.ObligationYear)
            .ThenByDescending(x => x.Updated)
            .ThenBy(x => x.Id);

        return Stream(query, cancellationToken);
    }

    public IAsyncEnumerable<AuditEvent> ReadAuditEvents(
        long? afterSequence,
        int limit,
        string? entity,
        string? entityId,
        CancellationToken cancellationToken
    )
    {
        var filters = new List<FilterDefinition<AuditEvent>>();
        if (afterSequence is not null)
        {
            filters.Add(Builders<AuditEvent>.Filter.Gt(x => x.Sequence, afterSequence.Value));
        }

        if (!string.IsNullOrWhiteSpace(entity))
        {
            filters.Add(Builders<AuditEvent>.Filter.Eq(x => x.Entity, entity.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(entityId))
        {
            filters.Add(Builders<AuditEvent>.Filter.Eq(x => x.EntityId, entityId.Trim()));
        }

        var filter = filters.Count == 0 ? Builders<AuditEvent>.Filter.Empty : Builders<AuditEvent>.Filter.And(filters);
        var query = _auditEvents.Find(filter).SortBy(x => x.Sequence).Limit(limit);

        return Stream(query, cancellationToken);
    }

    public async Task<OrganisationEligibilitySnapshot?> ReadEligibilitySnapshot(CancellationToken cancellationToken) =>
        await _eligibilitySnapshots
            .Find(x => x.Id == OrganisationEligibilitySnapshot.SnapshotId)
            .SingleOrDefaultAsync(cancellationToken);

    public async IAsyncEnumerable<OrganisationComplianceDeclarationEligibility> ReadEligibility(
        string? generation,
        OrganisationReferenceNumberResolutionState? referenceResolutionState,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var effectiveGeneration = generation;
        if (effectiveGeneration is null)
        {
            effectiveGeneration = (await ReadEligibilitySnapshot(cancellationToken))?.ActiveGeneration;
        }

        if (effectiveGeneration is null)
            yield break;

        var filter = Builders<OrganisationComplianceDeclarationEligibility>.Filter.Eq(
            x => x.Generation,
            effectiveGeneration
        );
        if (referenceResolutionState is not null)
        {
            filter &= Builders<OrganisationComplianceDeclarationEligibility>.Filter.Eq(
                x => x.ReferenceNumberResolutionState,
                referenceResolutionState.Value
            );
        }

        var query = _eligibilities
            .Find(filter)
            .SortBy(x => x.ReferenceNumberResolutionState)
            .ThenBy(x => x.OrganisationId)
            .ThenBy(x => x.ObligationYear)
            .ThenBy(x => x.RegistrationType);

        await foreach (var eligibility in Stream(query, cancellationToken))
        {
            yield return eligibility;
        }
    }

    public async IAsyncEnumerable<OrganisationComplianceDeclarationEligibility> ReadReferenceResolutionIssues(
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var activeGeneration = (await ReadEligibilitySnapshot(cancellationToken))?.ActiveGeneration;
        if (activeGeneration is null)
            yield break;

        var filter = Builders<OrganisationComplianceDeclarationEligibility>.Filter.And(
            Builders<OrganisationComplianceDeclarationEligibility>.Filter.Eq(x => x.Generation, activeGeneration),
            Builders<OrganisationComplianceDeclarationEligibility>.Filter.Ne(
                x => x.ReferenceNumberResolutionState,
                OrganisationReferenceNumberResolutionState.Resolved
            )
        );
        var query = _eligibilities
            .Find(filter)
            .SortBy(x => x.ReferenceNumberResolutionState)
            .ThenBy(x => x.OrganisationId)
            .ThenBy(x => x.ObligationYear)
            .ThenBy(x => x.RegistrationType);

        await foreach (var eligibility in Stream(query, cancellationToken))
        {
            yield return eligibility;
        }
    }

    public IAsyncEnumerable<OrganisationObligationSummary> ReadOrganisationObligationSummaries(
        Guid organisationId,
        CancellationToken cancellationToken
    )
    {
        var query = _obligationSummaries.Find(x => x.OrganisationId == organisationId).SortBy(x => x.ObligationYear);

        return Stream(query, cancellationToken);
    }

    public IAsyncEnumerable<OrganisationObligationSummary> ReadFailedOrganisationObligationSummaries(
        int obligationYear,
        CancellationToken cancellationToken
    )
    {
        var query = _obligationSummaries
            .Find(x =>
                x.ObligationYear == obligationYear && x.RefreshState == OrganisationObligationRefreshState.Failed
            )
            .SortBy(x => x.NextRefreshAt)
            .ThenBy(x => x.OrganisationId);

        return Stream(query, cancellationToken);
    }

    public IAsyncEnumerable<OrganisationObligationHistoricalBackfill> ReadHistoricalBackfills(
        CancellationToken cancellationToken
    )
    {
        var query = _historicalBackfills
            .Find(FilterDefinition<OrganisationObligationHistoricalBackfill>.Empty)
            .SortBy(x => x.CompletedAt)
            .ThenBy(x => x.RequestedAt);

        return Stream(query, cancellationToken);
    }

    public async Task<OrganisationObligationRequestPacingState?> ReadRequestPacingState(
        CancellationToken cancellationToken
    ) =>
        await _requestPacingStates
            .Find(x => x.Id == OrganisationObligationRequestPacingState.StateId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<AuditEventCounter?> ReadAuditEventCounter(CancellationToken cancellationToken) =>
        await _auditEventCounters.Find(x => x.Id == AuditEventCounterId).SingleOrDefaultAsync(cancellationToken);

    private static async IAsyncEnumerable<T> Stream<T>(
        IFindFluent<T, T> query,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        using var cursor = await query.ToCursorAsync(cancellationToken);
        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (var entity in cursor.Current)
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return entity;
            }
        }
    }
}
