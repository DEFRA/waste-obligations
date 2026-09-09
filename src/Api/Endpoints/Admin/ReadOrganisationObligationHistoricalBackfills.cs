using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.Admin;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public static class ReadOrganisationObligationHistoricalBackfills
{
    public const string OperationId = "ReadAdminOrganisationObligationHistoricalBackfills";

    public static void MapOrganisationObligationHistoricalBackfillsRead(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/unsubmitted-compliance-declarations/historical-backfills", Handle)
            .WithName(OperationId)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
    }

    private static IResult Handle(
        [FromServices] IAdminDataService adminDataService,
        CancellationToken cancellationToken
    ) =>
        new EntityStreamResult<OrganisationObligationHistoricalBackfill>(
            adminDataService.ReadHistoricalBackfills(cancellationToken)
        );
}
