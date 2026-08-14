using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Organisations.ComplianceDeclarations;

public static class ReadComplianceDeclarations
{
    public const string OperationId = "ReadOrganisationComplianceDeclarations";
    public const int OpenApiPageParameterIndex = 2;

    public static void MapComplianceDeclarationsRead(this IEndpointRouteBuilder app)
    {
        app.MapGet("/organisations/{organisationId:guid}/compliance-declarations", Handle)
            .WithName(OperationId)
            .WithTags("Compliance Declarations")
            .WithSummary("Compliance declarations for an organisation by year")
            .WithDescription(
                "Returns a paged list of compliance declarations for an organisation by organisation ID for the specified year"
            )
            .Produces<ComplianceDeclarationsPaged>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(PolicyNames.Read);
    }

    private static async Task<IResult> Handle(
        [FromRoute] Guid organisationId,
        [AsParameters] ReadComplianceDeclarationsRequest request,
        [FromServices] IWasteOrganisationsService wasteOrganisationsService,
        [FromServices] IComplianceDeclarationService complianceDeclarationService,
        CancellationToken cancellationToken
    )
    {
        var obligationYear = request.ObligationYearValue;
        var page = request.EffectivePage;
        var pageSize = request.EffectivePageSize;
        var organisationTask = wasteOrganisationsService.Read(organisationId, cancellationToken);
        var complianceDeclarationsTask = complianceDeclarationService.Read(
            organisationId,
            obligationYear,
            page,
            pageSize,
            cancellationToken
        );

        await Task.WhenAll(organisationTask, complianceDeclarationsTask);

        var organisation = await organisationTask;
        if (organisation is null)
            return Results.NotFound();

        var complianceDeclarations = await complianceDeclarationsTask;

        return Results.Ok(complianceDeclarations.ToDto(page, pageSize));
    }
}
