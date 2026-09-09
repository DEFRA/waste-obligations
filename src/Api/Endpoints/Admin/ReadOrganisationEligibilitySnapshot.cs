using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Services.Admin;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public static class ReadOrganisationEligibilitySnapshot
{
    public const string OperationId = "ReadAdminOrganisationEligibilitySnapshot";

    public static void MapOrganisationEligibilitySnapshotRead(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/unsubmitted-compliance-declarations/eligibility-snapshot", Handle)
            .WithName(OperationId)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
    }

    private static async Task<IResult> Handle(
        [FromServices] IAdminDataService adminDataService,
        CancellationToken cancellationToken
    )
    {
        var snapshot = await adminDataService.ReadEligibilitySnapshot(cancellationToken);

        return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
    }
}
