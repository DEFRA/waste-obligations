using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Microsoft.AspNetCore.Mvc;
using ComplianceDeclaration = Defra.WasteObligations.Api.Dtos.ComplianceDeclaration;

namespace Defra.WasteObligations.Api.Endpoints.Organisations.ComplianceDeclarations;

public static class CreateComplianceDeclaration
{
    public static void MapComplianceDeclarationsCreate(this IEndpointRouteBuilder app)
    {
        app.MapPost("/organisations/{organisationId:guid}/compliance-declarations", Handle)
            .WithName("CreateOrganisationComplianceDeclaration")
            .WithTags("ComplianceDeclarations")
            .WithSummary("Create a compliance declaration")
            .WithDescription("Create a compliance declaration for the specified organisation ID")
            .Produces<ComplianceDeclaration>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(PolicyNames.Write);
    }

    [HttpGet]
    private static async Task<IResult> Handle(
        [FromRoute] Guid organisationId,
        [FromBody] CreateComplianceDeclarationRequest request,
        [FromServices] IWasteOrganisationsService wasteOrganisationsService,
        [FromServices] IComplianceDeclarationService complianceDeclarationService,
        CancellationToken cancellationToken
    )
    {
        var organisation = await wasteOrganisationsService.Read(organisationId, cancellationToken);
        if (organisation is null)
            return Results.NotFound();

        var complianceDeclaration = await complianceDeclarationService.Create(
            request.ToEntity(organisation),
            cancellationToken
        );

        return Results.Created(
            $"/organisations/{organisationId:D}/compliance-declarations/{complianceDeclaration.Id:D}",
            complianceDeclaration.ToDto()
        );
    }
}
