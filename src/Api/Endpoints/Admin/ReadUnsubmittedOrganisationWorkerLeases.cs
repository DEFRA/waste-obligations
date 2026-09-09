using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.Admin;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public static class ReadUnsubmittedOrganisationWorkerLeases
{
    public const string OperationId = "ReadAdminUnsubmittedOrganisationWorkerLeases";

    public static void MapUnsubmittedOrganisationWorkerLeasesRead(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/unsubmitted-compliance-declarations/worker-leases", Handle)
            .WithName(OperationId)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
    }

    private static IResult Handle(
        [FromServices] IAdminDataService adminDataService,
        CancellationToken cancellationToken
    ) => new EntityStreamResult<BackgroundWorkerLease>(adminDataService.ReadWorkerLeases(cancellationToken));
}
