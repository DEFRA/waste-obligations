using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Microsoft.AspNetCore.Mvc;
using ComplianceDeclaration = Defra.WasteObligations.Api.Dtos.ComplianceDeclaration;

namespace Defra.WasteObligations.Api.Endpoints.Organisations.ComplianceDeclarations;

public static class UpdateComplianceDeclaration
{
    public static void MapComplianceDeclarationUpdate(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/organisations/{organisationId:guid}/compliance-declarations/{complianceDeclarationId}", Handle)
            .WithName("UpdateOrganisationComplianceDeclaration")
            .WithTags("Compliance Declarations")
            .WithSummary("Update compliance declaration by ID")
            .WithDescription("Update a compliance declaration by compliance declaration ID")
            .Produces<ComplianceDeclaration>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(PolicyNames.Write);
    }

    private static async Task<IResult> Handle(
        [FromRoute] Guid organisationId,
        [FromRoute] string complianceDeclarationId,
        [FromBody] UpdateComplianceDeclarationRequest request,
        [FromServices] IWasteOrganisationsService wasteOrganisationsService,
        [FromServices] IComplianceDeclarationService complianceDeclarationService,
        [FromServices] TimeProvider timeProvider,
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

        if (request.Status.HasValue)
        {
            complianceDeclaration = complianceDeclaration.UpdateStatus(
                request.Status.Value.ToEntity(),
                request.Reason,
                request.User.ToEntity(),
                timeProvider.GetUtcNowWithoutMicroseconds()
            );

            complianceDeclaration = await complianceDeclarationService.Update(complianceDeclaration, cancellationToken);
        }

        return Results.Ok(complianceDeclaration.ToDto());
    }
}
