using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Organisations.Obligations;

public static class ReadObligations
{
    public static void MapObligationsRead(this IEndpointRouteBuilder app)
    {
        app.MapGet("/organisations/{organisationId:guid}/obligations", Handle)
            .WithName("ReadOrganisationObligations")
            .WithTags("Obligations")
            .WithSummary("Obligations for an organisation by year")
            .WithDescription("Returns the obligations for an organisation by organisation ID for the specified year")
            .Produces<OrganisationObligations>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(PolicyNames.Read);
    }

    [HttpGet]
    private static async Task<IResult> Handle(
        [FromRoute] Guid organisationId,
        [AsParameters] ReadObligationsRequest request,
        [FromServices] IWasteOrganisationsService wasteOrganisationsService,
        [FromServices] IPrnCommonBackendService prnCommonBackendService,
        CancellationToken cancellationToken
    )
    {
        var obligationYear = request.ObligationYearValue;
        var organisationTask = wasteOrganisationsService.Read(organisationId, cancellationToken);
        var obligationsTask = prnCommonBackendService.ReadObligations(
            organisationId,
            obligationYear,
            cancellationToken
        );

        await Task.WhenAll(organisationTask, obligationsTask);

        var organisation = await organisationTask;
        if (organisation is null)
            return Results.NotFound();

        var obligations = await obligationsTask;

        return Results.Ok(new OrganisationObligations { Obligations = obligations.Select(x => x.ToDto()).ToArray() });
    }
}
