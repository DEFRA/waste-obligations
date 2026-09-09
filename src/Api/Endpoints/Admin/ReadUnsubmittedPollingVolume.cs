using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public static class ReadUnsubmittedPollingVolume
{
    public const string OperationId = "ReadUnsubmittedPollingVolume";

    public static void MapUnsubmittedPollingVolume(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/unsubmitted-compliance-declarations/polling-volume", Handle)
            .WithName(OperationId)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
    }

    private static async Task<IResult> Handle(
        [FromServices] IUnsubmittedPollingVolumeService pollingVolumeService,
        CancellationToken cancellationToken
    ) => Results.Ok(await pollingVolumeService.Get(cancellationToken));
}
