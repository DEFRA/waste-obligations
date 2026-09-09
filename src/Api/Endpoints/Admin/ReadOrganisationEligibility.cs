using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.Admin;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public static class ReadOrganisationEligibility
{
    public const string OperationId = "ReadAdminOrganisationEligibility";

    public static void MapOrganisationEligibilityRead(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/unsubmitted-compliance-declarations/eligibility", Handle)
            .WithName(OperationId)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
    }

    private static IResult Handle(
        [AsParameters] ReadOrganisationEligibilityRequest request,
        [FromServices] IAdminDataService adminDataService,
        CancellationToken cancellationToken
    ) =>
        new EntityStreamResult<OrganisationComplianceDeclarationEligibility>(
            adminDataService.ReadEligibility(
                request.Generation,
                request.ParsedReferenceResolutionState(),
                cancellationToken
            )
        );
}
