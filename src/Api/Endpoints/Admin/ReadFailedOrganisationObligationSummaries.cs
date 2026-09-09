using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.Admin;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public static class ReadFailedOrganisationObligationSummaries
{
    public const string OperationId = "ReadAdminFailedOrganisationObligationSummaries";

    public static void MapFailedOrganisationObligationSummariesRead(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/unsubmitted-compliance-declarations/failed-obligation-summaries", Handle)
            .WithName(OperationId)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
    }

    private static IResult Handle(
        [AsParameters] ReadFailedOrganisationObligationSummariesRequest request,
        [FromServices] IAdminDataService adminDataService,
        CancellationToken cancellationToken
    ) =>
        new EntityStreamResult<OrganisationObligationSummary>(
            adminDataService.ReadFailedOrganisationObligationSummaries(request.ObligationYear!.Value, cancellationToken)
        );
}
