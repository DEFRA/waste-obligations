using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Organisations.Prns;

public static class UpdatePrn
{
    public static void MapPrnUpdate(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/organisations/{organisationId:guid}/prns/{prnId}", Handle)
            .WithName("UpdateOrganisationPrn")
            .WithTags("PRNs")
            .WithSummary("Update PRN status by ID")
            .WithDescription("Accept or reject a PRN by PRN ID")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(PolicyNames.Write);
    }

    private static async Task<IResult> Handle(
        [FromRoute] Guid organisationId,
        [FromRoute] string prnId,
        [FromBody] UpdatePrnRequest request,
        [FromServices] IWasteOrganisationsService wasteOrganisationsService,
        [FromServices] IPrnCommonBackendService prnCommonBackendService,
        CancellationToken cancellationToken
    )
    {
        var organisation = await wasteOrganisationsService.Read(organisationId, cancellationToken);
        if (organisation is null)
            return Results.NotFound();

        var result = await prnCommonBackendService.UpdatePrnStatus(
            organisationId,
            request.User.EffectiveId,
            prnId,
            request.Status switch
            {
                UpdatePrnStatus.Accepted => "ACCEPTED",
                UpdatePrnStatus.Rejected => "REJECTED",
                _ => throw new ArgumentOutOfRangeException(nameof(request)),
            },
            cancellationToken
        );

        return result switch
        {
            PrnStatusUpdateResult.Updated => Results.Ok(),
            PrnStatusUpdateResult.NotFound => Results.NotFound(),
            _ => throw new ArgumentOutOfRangeException(nameof(request)),
        };
    }
}
