using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Organisations.ComplianceDeclarations;

public static class ReadComplianceDeclarations
{
    public static void MapComplianceDeclarationsRead(this IEndpointRouteBuilder app)
    {
        app.MapGet("/organisations/{organisationId:guid}/compliance-declarations", Handle)
            .WithName("ReadOrganisationComplianceDeclarations")
            .WithTags("ComplianceDeclarations")
            .WithSummary("Compliance declarations for an organisation by year")
            .WithDescription(
                "Returns the compliance declarations for an organisation by organisation ID for the specified year"
            )
            .Produces<OrganisationComplianceDeclarations>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(PolicyNames.Read);
    }

    [HttpGet]
    private static async Task<IResult> Handle(
        [FromRoute] Guid organisationId,
        [AsParameters] ReadComplianceDeclarationsRequest request,
        [FromServices] IWasteOrganisationsService wasteOrganisationsService,
        [FromServices] IComplianceDeclarationService complianceDeclarationService,
        CancellationToken cancellationToken
    )
    {
        var obligationYear = request.ObligationYearValue;
        var organisationTask = wasteOrganisationsService.Read(organisationId, cancellationToken);
        var complianceDeclarationsTask = complianceDeclarationService.Read(
            organisationId,
            obligationYear,
            cancellationToken
        );

        await Task.WhenAll(organisationTask, complianceDeclarationsTask);

        var organisation = await organisationTask;
        if (organisation is null)
            return Results.NotFound();

        var complianceDeclarations = await complianceDeclarationsTask;

        return Results.Ok(
            new OrganisationComplianceDeclarations
            {
                ComplianceDeclarations = complianceDeclarations
                    .OrderByDescending(x => x.Updated)
                    .Select(x => x.ToDto()),
            }
        );
    }
}
