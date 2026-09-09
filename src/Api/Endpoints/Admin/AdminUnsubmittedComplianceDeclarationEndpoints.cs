using Defra.WasteObligations.Api.Authentication;
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
        app.MapGet("/admin/unsubmitted-compliance-declarations/polling-status", HandlePollingStatus)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
        app.MapGet("/admin/unsubmitted-compliance-declarations/polling-volume", HandlePollingVolume)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
        app.MapGet("/admin/unsubmitted-compliance-declarations/polling-plan", HandlePollingPlan)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
        app.MapPost("/admin/unsubmitted-compliance-declarations/historical-backfill", HandleHistoricalBackfillStart)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
        app.MapGet(
                "/admin/unsubmitted-compliance-declarations/reference-resolution-issues",
                HandleReferenceResolutionIssues
            )
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
        app.MapGet(
                "/admin/unsubmitted-compliance-declarations/organisations/{organisationId:guid}",
                HandleOrganisationDetails
            )
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
        app.MapGet("/admin/unsubmitted-compliance-declarations/eligibility-snapshot", HandleEligibilitySnapshot)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
        app.MapGet("/admin/unsubmitted-compliance-declarations/eligibility", HandleEligibility)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
        app.MapGet(
                "/admin/unsubmitted-compliance-declarations/failed-obligation-summaries",
                HandleFailedObligationSummaries
            )
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
        app.MapGet("/admin/unsubmitted-compliance-declarations/historical-backfills", HandleHistoricalBackfills)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
        app.MapGet("/admin/unsubmitted-compliance-declarations/request-pacing", HandleRequestPacingState)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
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

    private static IResult HandleReferenceResolutionIssues(
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

    private static IResult HandleEligibility(
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

    private static IResult HandleFailedObligationSummaries(
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

    private static IResult HandleHistoricalBackfills(
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
