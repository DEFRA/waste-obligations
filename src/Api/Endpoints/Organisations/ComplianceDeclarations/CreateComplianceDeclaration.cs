using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Organisations.ComplianceDeclarations;

public static class CreateComplianceDeclaration
{
    public static void MapComplianceDeclarationsCreate(this IEndpointRouteBuilder app)
    {
        app.MapPost("/organisations/{id:guid}/compliance-declarations", Handle)
            .WithName("CreateOrganisationComplianceDeclaration")
            .WithTags("ComplianceDeclarations")
            .WithSummary("Create a compliance declaration")
            .WithDescription("Create a compliance declaration for the specified organisation ID")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(PolicyNames.Write);
    }

    [HttpGet]
    private static async Task<IResult> Handle(
        [FromRoute] Guid id,
        [FromBody] CreateComplianceDeclarationRequest request,
        [FromServices] IOrganisationService organisationService,
        CancellationToken cancellationToken
    )
    {
        var organisation = await organisationService.ReadOrganisation(id, cancellationToken);
        if (organisation is null)
            return Results.NotFound();

        return Results.Created($"/organisations/{id:D}/compliance-declarations/[newid]", null);
    }
}
