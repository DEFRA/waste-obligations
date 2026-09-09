using System.ComponentModel.DataAnnotations;
using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services.Admin;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public static class ReadOrganisationComplianceDeclarations
{
    public const string OperationId = "ReadAdminOrganisationComplianceDeclarations";

    public static void MapOrganisationComplianceDeclarationsRead(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/organisations/{organisationId:guid}/compliance-declarations", Handle)
            .WithName(OperationId)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
    }

    private static IResult Handle(
        [FromRoute] Guid organisationId,
        [FromQuery] [Range(ObligationYear.Minimum, ObligationYear.Maximum)] int? obligationYear,
        [FromServices] IAdminDataService adminDataService,
        CancellationToken cancellationToken
    ) =>
        new EntityStreamResult<Data.Entities.ComplianceDeclaration>(
            adminDataService.ReadComplianceDeclarations(organisationId, obligationYear, cancellationToken)
        );
}
