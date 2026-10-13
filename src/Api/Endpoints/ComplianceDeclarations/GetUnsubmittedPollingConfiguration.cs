using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.OrganisationEligibility;
using Defra.WasteObligations.Api.Services.OrganisationObligations;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Endpoints.ComplianceDeclarations;

public static class GetUnsubmittedPollingConfiguration
{
    public const string OperationId = "GetUnsubmittedPollingConfiguration";

    public static void MapUnsubmittedPollingConfiguration(this IEndpointRouteBuilder app)
    {
        app.MapGet("/compliance-declarations/unsubmitted/polling-configuration", Handle)
            .WithName(OperationId)
            .WithTags("Search")
            .WithSummary("Get unsubmitted organisation polling configuration")
            .WithDescription("Returns effective polling timings and the current obligation year")
            .Produces<UnsubmittedPollingConfiguration>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(PolicyNames.Read);
    }

    private static IResult Handle(
        IOptions<OrganisationEligibilityOptions> eligibility,
        IOptions<OrganisationObligationHydrationOptions> hydration,
        ICurrentObligationYearProvider yearProvider
    )
    {
        return Results.Ok(
            new UnsubmittedPollingConfiguration
            {
                EligibilityRefreshPollingEnabled = eligibility.Value.RefreshPollingEnabled,
                OrganisationPollIntervalSeconds = eligibility.Value.RefreshPollIntervalSeconds,
                ObligationPollingEnabled = hydration.Value.PollingEnabled,
                ObligationRefreshIntervalSeconds = hydration.Value.RefreshInterval.TotalSeconds,
                MaximumSummaryStalenessSeconds = hydration.Value.MaximumSummaryStaleness.TotalSeconds,
                MaxDownstreamRequestsPerMinute = hydration.Value.MaxDownstreamRequestsPerMinute,
                ObligationPollIntervalSeconds = hydration.Value.PollIntervalSeconds,
                CurrentObligationYear = yearProvider.GetCurrentObligationYear(),
            }
        );
    }
}
