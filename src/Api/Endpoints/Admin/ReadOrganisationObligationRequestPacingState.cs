using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Services.Admin;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public static class ReadOrganisationObligationRequestPacingState
{
    public const string OperationId = "ReadAdminOrganisationObligationRequestPacingState";

    public static void MapOrganisationObligationRequestPacingStateRead(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/unsubmitted-compliance-declarations/request-pacing", Handle)
            .WithName(OperationId)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
    }

    private static async Task<IResult> Handle(
        [FromServices] IAdminDataService adminDataService,
        CancellationToken cancellationToken
    )
    {
        var requestPacingState = await adminDataService.ReadRequestPacingState(cancellationToken);

        return requestPacingState is null ? Results.NotFound() : Results.Ok(requestPacingState);
    }
}
