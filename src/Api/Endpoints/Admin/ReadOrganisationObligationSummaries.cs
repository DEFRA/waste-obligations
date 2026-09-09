using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.Admin;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public static class ReadOrganisationObligationSummaries
{
    public const string OperationId = "ReadAdminOrganisationObligationSummaries";

    public static void MapOrganisationObligationSummariesRead(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/organisations/{organisationId:guid}/obligation-summaries", Handle)
            .WithName(OperationId)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
    }

    private static IResult Handle(
        [FromRoute] Guid organisationId,
        [FromServices] IAdminDataService adminDataService,
        CancellationToken cancellationToken
    ) =>
        new EntityStreamResult<OrganisationObligationSummary>(
            adminDataService.ReadOrganisationObligationSummaries(organisationId, cancellationToken)
        );
}
