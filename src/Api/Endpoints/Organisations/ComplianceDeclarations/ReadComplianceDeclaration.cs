using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Microsoft.AspNetCore.Mvc;
using ComplianceDeclaration = Defra.WasteObligations.Api.Dtos.ComplianceDeclaration;

namespace Defra.WasteObligations.Api.Endpoints.Organisations.ComplianceDeclarations;

public static class ReadComplianceDeclaration
{
    public static void MapComplianceDeclarationRead(this IEndpointRouteBuilder app)
    {
        app.MapGet("/organisations/{organisationId:guid}/compliance-declarations/{complianceDeclarationId}", Handle)
            .WithName("ReadOrganisationComplianceDeclaration")
            .WithTags("Compliance Declarations")
            .WithSummary("Compliance declaration by ID")
            .WithDescription("Return a compliance declaration by compliance declaration ID")
            .Produces<ComplianceDeclaration>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(PolicyNames.Read);
    }

    private static async Task<IResult> Handle(
        [FromRoute] Guid organisationId,
        [FromRoute] string complianceDeclarationId,
        [FromServices] IWasteOrganisationsService wasteOrganisationsService,
        [FromServices] IComplianceDeclarationService complianceDeclarationService,
        CancellationToken cancellationToken
    )
    {
        var organisationTask = wasteOrganisationsService.Read(organisationId, cancellationToken);
        var complianceDeclarationTask = complianceDeclarationService.Read(complianceDeclarationId, cancellationToken);

        await Task.WhenAll(organisationTask, complianceDeclarationTask);

        var organisation = await organisationTask;
        if (organisation is null)
            return Results.NotFound();

        var complianceDeclaration = await complianceDeclarationTask;
        if (complianceDeclaration is null || complianceDeclaration.Organisation.Id != organisation.Id)
            return Results.NotFound();

        return Results.Ok(complianceDeclaration.ToDto());
    }
}
