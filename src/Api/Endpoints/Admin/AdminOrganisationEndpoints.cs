using System.ComponentModel.DataAnnotations;
using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.Admin;
using Microsoft.AspNetCore.Mvc;
using Dtos = Defra.WasteObligations.Api.Dtos;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public static class AdminOrganisationEndpoints
{
    public static void MapAdminOrganisationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/organisations/{organisationId:guid}/compliance-declarations", HandleComplianceDeclarations)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
        app.MapGet("/admin/organisations/{organisationId:guid}/obligation-summaries", HandleObligationSummaries)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
    }

    private static IResult HandleComplianceDeclarations(
        [FromRoute] Guid organisationId,
        [FromQuery] [Range(Dtos.ObligationYear.Minimum, Dtos.ObligationYear.Maximum)] int? obligationYear,
        [FromServices] IAdminDataService adminDataService,
        CancellationToken cancellationToken
    ) =>
        new EntityStreamResult<ComplianceDeclaration>(
            adminDataService.ReadComplianceDeclarations(organisationId, obligationYear, cancellationToken)
        );

    private static IResult HandleObligationSummaries(
        [FromRoute] Guid organisationId,
        [FromServices] IAdminDataService adminDataService,
        CancellationToken cancellationToken
    ) =>
        new EntityStreamResult<OrganisationObligationSummary>(
            adminDataService.ReadOrganisationObligationSummaries(organisationId, cancellationToken)
        );
}
