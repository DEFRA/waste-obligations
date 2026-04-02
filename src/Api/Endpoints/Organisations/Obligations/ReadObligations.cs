using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Organisations.Obligations;

public static class ReadObligations
{
    public static void MapObligationsRead(this IEndpointRouteBuilder app)
    {
        app.MapGet("/organisations/{id:guid}/obligations", Handle)
            .WithName("ReadOrganisationObligations")
            .WithTags("Obligations")
            .WithSummary("Obligations for an organisation by year")
            .WithDescription("Returns the obligations for an organisation by organisation ID for the specified year")
            .Produces<OrganisationObligations>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(PolicyNames.Read);
    }

    [HttpGet]
    private static async Task<IResult> Handle(
        [FromRoute] Guid id,
        [AsParameters] OrganisationObligationsRequest request,
        [FromServices] IOrganisationService organisationService,
        CancellationToken cancellationToken
    )
    {
        var organisation = await organisationService.ReadOrganisation(id, cancellationToken);
        if (organisation is null)
            return Results.NotFound();

        var obligations = await organisationService.ReadObligations(
            id,
            request.Year.GetValueOrDefault(),
            cancellationToken
        );

        return Results.Ok(
            new OrganisationObligations
            {
                Obligations = obligations.Select(x => x.ToDto()).ToArray(),
                Organisation = request.Include == IncludeTypes.Organisation ? organisation.ToDto() : null,
            }
        );
    }
}
