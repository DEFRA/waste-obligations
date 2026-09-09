namespace Defra.WasteObligations.Api.Endpoints.Admin;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapOrganisationComplianceDeclarationsRead();
        app.MapAuditEventsRead();
        app.MapAuditEventCounterRead();
        app.MapAuditEventDispatchLeaseRead();
        app.MapMongoMigrationLeaseRead();
        app.MapUnsubmittedPollingStatus();
        app.MapUnsubmittedPollingVolume();
        app.MapUnsubmittedPollingPlan();
        app.MapUnsubmittedHistoricalBackfill();
        app.MapUnsubmittedReferenceResolutionIssues();
        app.MapUnsubmittedOrganisationDetails();
        app.MapOrganisationEligibilitySnapshotRead();
        app.MapOrganisationEligibilityRead();
        app.MapOrganisationObligationSummariesRead();
        app.MapFailedOrganisationObligationSummariesRead();
        app.MapOrganisationObligationHistoricalBackfillsRead();
        app.MapOrganisationObligationRequestPacingStateRead();
        app.MapUnsubmittedOrganisationWorkerLeasesRead();
    }
}
