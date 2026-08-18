using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Organisations.Prns;

public static class ReadPrn
{
    public static void MapPrnRead(this IEndpointRouteBuilder app)
    {
        app.MapGet("/organisations/{organisationId:guid}/prns/{prnId}", Handle)
            .WithName("ReadOrganisationPrn")
            .WithTags("PRNs")
            .WithSummary("PRN by ID")
            .WithDescription("Return a PRN by organisation ID and PRN ID")
            .Produces<Prn>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(PolicyNames.Read);
    }

    private static async Task<IResult> Handle(
        [FromRoute] Guid organisationId,
        [FromRoute] string prnId,
        [FromServices] IWasteOrganisationsService wasteOrganisationsService,
        [FromServices] IPrnCommonBackendService prnCommonBackendService,
        CancellationToken cancellationToken
    )
    {
        var organisationTask = wasteOrganisationsService.Read(organisationId, cancellationToken);
        var prnDetailsTask = prnCommonBackendService.ReadPrn(organisationId, prnId, cancellationToken);

        await Task.WhenAll(organisationTask, prnDetailsTask);

        var organisation = await organisationTask;
        var prnDetails = await prnDetailsTask;

        if (organisation is null || prnDetails is null)
            return Results.NotFound();

        var prn = prnDetails.ToDto();

        if (prn.Recipient.OrganisationId != organisationId)
            return Results.NotFound();

        return Results.Ok(prn);
    }
}
