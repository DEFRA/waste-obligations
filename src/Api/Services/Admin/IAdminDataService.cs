using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.AuditEvents.Entities;

namespace Defra.WasteObligations.Api.Services.Admin;

public interface IAdminDataService
{
    IAsyncEnumerable<ComplianceDeclaration> ReadComplianceDeclarations(
        Guid organisationId,
        int? obligationYear,
        CancellationToken cancellationToken
    );

    IAsyncEnumerable<AuditEvent> ReadAuditEvents(
        long? afterSequence,
        int limit,
        string? entity,
        string? entityId,
        CancellationToken cancellationToken
    );

    Task<OrganisationEligibilitySnapshot?> ReadEligibilitySnapshot(CancellationToken cancellationToken);

    IAsyncEnumerable<OrganisationComplianceDeclarationEligibility> ReadEligibility(
        string? generation,
        OrganisationReferenceNumberResolutionState? referenceResolutionState,
        CancellationToken cancellationToken
    );

    IAsyncEnumerable<OrganisationComplianceDeclarationEligibility> ReadReferenceResolutionIssues(
        CancellationToken cancellationToken
    );

    IAsyncEnumerable<OrganisationObligationSummary> ReadOrganisationObligationSummaries(
        Guid organisationId,
        CancellationToken cancellationToken
    );

    IAsyncEnumerable<OrganisationObligationSummary> ReadFailedOrganisationObligationSummaries(
        int obligationYear,
        CancellationToken cancellationToken
    );

    IAsyncEnumerable<OrganisationObligationHistoricalBackfill> ReadHistoricalBackfills(
        CancellationToken cancellationToken
    );

    Task<OrganisationObligationRequestPacingState?> ReadRequestPacingState(CancellationToken cancellationToken);

    IAsyncEnumerable<BackgroundWorkerLease> ReadWorkerLeases(CancellationToken cancellationToken);

    Task<AuditEventCounter?> ReadAuditEventCounter(CancellationToken cancellationToken);

    Task<AuditEventDispatchLease?> ReadAuditEventDispatchLease(string processName, CancellationToken cancellationToken);

    Task<MongoMigrationLease?> ReadMongoMigrationLease(CancellationToken cancellationToken);
}
