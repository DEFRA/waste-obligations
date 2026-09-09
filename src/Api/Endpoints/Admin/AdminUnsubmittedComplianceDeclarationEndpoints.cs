using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.Admin;
using Defra.WasteObligations.Api.Services.OrganisationObligations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Dtos = Defra.WasteObligations.Api.Dtos;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public static class AdminUnsubmittedComplianceDeclarationEndpoints
{
    public static void MapAdminUnsubmittedComplianceDeclarationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("unsubmitted-compliance-declarations/polling-status", HandlePollingStatus).ExcludeFromDescription();
        app.MapGet("unsubmitted-compliance-declarations/polling-volume", HandlePollingVolume).ExcludeFromDescription();
        app.MapGet("unsubmitted-compliance-declarations/polling-plan", HandlePollingPlan).ExcludeFromDescription();
        app.MapPost("unsubmitted-compliance-declarations/historical-backfill", HandleHistoricalBackfillStart)
            .ExcludeFromDescription();
        app.MapGet("unsubmitted-compliance-declarations/reference-resolution-issues", HandleReferenceResolutionIssues)
            .ExcludeFromDescription();
        app.MapGet("unsubmitted-compliance-declarations/organisations/{organisationId:guid}", HandleOrganisationDetails)
            .ExcludeFromDescription();
        app.MapGet("unsubmitted-compliance-declarations/eligibility-snapshot", HandleEligibilitySnapshot)
            .ExcludeFromDescription();
        app.MapGet("unsubmitted-compliance-declarations/eligibility", HandleEligibility).ExcludeFromDescription();
        app.MapGet("unsubmitted-compliance-declarations/failed-obligation-summaries", HandleFailedObligationSummaries)
            .ExcludeFromDescription();
        app.MapGet("unsubmitted-compliance-declarations/historical-backfills", HandleHistoricalBackfills)
            .ExcludeFromDescription();
        app.MapGet("unsubmitted-compliance-declarations/request-pacing", HandleRequestPacingState)
            .ExcludeFromDescription();
    }

    private static async Task<IResult> HandlePollingStatus(
        [FromServices] IUnsubmittedPollingStatusService pollingStatusService,
        CancellationToken cancellationToken
    ) => Results.Ok(await pollingStatusService.Get(cancellationToken));

    private static async Task<IResult> HandlePollingVolume(
        [FromServices] IUnsubmittedPollingVolumeService pollingVolumeService,
        CancellationToken cancellationToken
    ) => Results.Ok(await pollingVolumeService.Get(cancellationToken));

    private static async Task<IResult> HandlePollingPlan(
        [FromServices] IUnsubmittedPollingPlanService pollingPlanService,
        CancellationToken cancellationToken
    ) => Results.Ok(await pollingPlanService.Get(cancellationToken));

    private static async Task<IResult> HandleHistoricalBackfillStart(
        [FromServices] IUnsubmittedHistoricalBackfillService historicalBackfillService,
        [FromServices] IOptions<OrganisationObligationHydrationOptions> options,
        CancellationToken cancellationToken
    )
    {
        if (options.Value.PollingEnabled)
            return Results.Conflict();

        return Results.Ok(await historicalBackfillService.Start(cancellationToken));
    }

    private static EntityStreamResult<OrganisationComplianceDeclarationEligibility> HandleReferenceResolutionIssues(
        [FromServices] IAdminDataService adminDataService,
        CancellationToken cancellationToken
    ) =>
        new EntityStreamResult<OrganisationComplianceDeclarationEligibility>(
            adminDataService.ReadReferenceResolutionIssues(cancellationToken)
        );

    private static async Task<IResult> HandleOrganisationDetails(
        [FromRoute] Guid organisationId,
        [FromServices] IUnsubmittedOrganisationDetailsService organisationDetailsService,
        CancellationToken cancellationToken,
        [FromQuery] bool includeLiveData = false
    )
    {
        var organisationDetails = await organisationDetailsService.Get(
            organisationId,
            includeLiveData,
            cancellationToken
        );

        return organisationDetails is null
            ? Results.NotFound()
            : new EntityResult<Dtos.UnsubmittedOrganisationDetails>(organisationDetails);
    }

    private static async Task<IResult> HandleEligibilitySnapshot(
        [FromServices] IAdminDataService adminDataService,
        CancellationToken cancellationToken
    )
    {
        var snapshot = await adminDataService.ReadEligibilitySnapshot(cancellationToken);

        return snapshot is null ? Results.NotFound() : new EntityResult<OrganisationEligibilitySnapshot>(snapshot);
    }

    private static EntityStreamResult<OrganisationComplianceDeclarationEligibility> HandleEligibility(
        [AsParameters] ReadOrganisationEligibilityRequest request,
        [FromServices] IAdminDataService adminDataService,
        CancellationToken cancellationToken
    ) =>
        new EntityStreamResult<OrganisationComplianceDeclarationEligibility>(
            adminDataService.ReadEligibility(
                request.Generation,
                request.ParsedReferenceResolutionState(),
                cancellationToken
            )
        );

    private static EntityStreamResult<OrganisationObligationSummary> HandleFailedObligationSummaries(
        [AsParameters] ReadFailedOrganisationObligationSummariesRequest request,
        [FromServices] IAdminDataService adminDataService,
        CancellationToken cancellationToken
    ) =>
        new EntityStreamResult<OrganisationObligationSummary>(
            adminDataService.ReadFailedOrganisationObligationSummaries(
                request.ObligationYear.GetValueOrDefault(),
                cancellationToken
            )
        );

    private static EntityStreamResult<OrganisationObligationHistoricalBackfill> HandleHistoricalBackfills(
        [FromServices] IAdminDataService adminDataService,
        CancellationToken cancellationToken
    ) =>
        new EntityStreamResult<OrganisationObligationHistoricalBackfill>(
            adminDataService.ReadHistoricalBackfills(cancellationToken)
        );

    private static async Task<IResult> HandleRequestPacingState(
        [FromServices] IAdminDataService adminDataService,
        CancellationToken cancellationToken
    )
    {
        var requestPacingState = await adminDataService.ReadRequestPacingState(cancellationToken);

        return requestPacingState is null
            ? Results.NotFound()
            : new EntityResult<OrganisationObligationRequestPacingState>(requestPacingState);
    }
}
