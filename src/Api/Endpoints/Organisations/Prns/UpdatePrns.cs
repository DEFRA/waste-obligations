using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Organisations.Prns;

public static class UpdatePrns
{
    public static void MapPrnsUpdate(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/organisations/{organisationId:guid}/prns", Handle)
            .WithName("UpdateOrganisationPrns")
            .WithTags("PRNs")
            .WithSummary("Update the status of several PRNs")
            .WithDescription(
                "Accept or reject several PRNs by PRN ID in one request. The PRN common backend applies the update "
                    + "to every PRN or to none: if any PRN is not found, or any PRN's current status is a different "
                    + "accepted or rejected status, no PRN is updated. A PRN already in the requested status is "
                    + "updated again. obligationYear may only be given when accepting; it is recorded as the year "
                    + "each PRN counts towards, as sent: the caller is responsible for choosing the right year. When "
                    + "it is given, every PRN must still be awaiting acceptance (409 otherwise)."
            )
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
        [FromBody] UpdatePrnsRequest request,
        [FromServices] IWasteOrganisationsService wasteOrganisationsService,
        [FromServices] IPrnCommonBackendService prnCommonBackendService,
        CancellationToken cancellationToken
    )
    {
        var organisation = await wasteOrganisationsService.Read(organisationId, cancellationToken);
        if (organisation is null)
            return Results.NotFound();

        // A year is recorded only on PRNs still awaiting acceptance. The common backend checks
        // that in the same write, so no read is needed here.
        var expectedStatus = request.ObligationYear is null ? null : PrnCommonBackendStatus.AwaitingAcceptance;

        var result = await prnCommonBackendService.UpdatePrnStatuses(
            organisationId,
            request.User.EffectiveId,
            request.EffectivePrnIds,
            request.Status.ToPrnCommonBackendStatus(),
            request.ObligationYear,
            new PrnStatusUpdateOptions(expectedStatus, PrnStatusConflict.Message),
            cancellationToken
        );

        return result.ToResult();
    }
}
