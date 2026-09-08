using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public static class ReadUnsubmittedPollingPlan
{
    public const string OperationId = "ReadUnsubmittedPollingPlan";

    public static void MapUnsubmittedPollingPlan(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/unsubmitted-compliance-declarations/polling-plan", Handle)
            .WithName(OperationId)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
    }

    private static async Task<IResult> Handle(
        [FromServices] IUnsubmittedPollingPlanService pollingPlanService,
        CancellationToken cancellationToken
    ) => Results.Ok(await pollingPlanService.Get(cancellationToken));
}
