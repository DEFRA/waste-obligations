using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public static class ReadUnsubmittedPollingStatus
{
    public const string OperationId = "ReadUnsubmittedPollingStatus";

    public static void MapUnsubmittedPollingStatus(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/unsubmitted-compliance-declarations/polling-status", Handle)
            .WithName(OperationId)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
    }

    private static async Task<IResult> Handle(
        [FromServices] IUnsubmittedPollingStatusService pollingStatusService,
        CancellationToken cancellationToken
    ) => Results.Ok(await pollingStatusService.Get(cancellationToken));
}
