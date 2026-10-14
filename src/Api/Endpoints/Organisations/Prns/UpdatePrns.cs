using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Data;
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

        if (request.ObligationYear is not null)
        {
            var precondition = await CheckPrnsAreAwaitingAcceptance(
                organisationId,
                request.EffectivePrnIds,
                prnCommonBackendService,
                cancellationToken
            );

            if (precondition is not null)
                return precondition;
        }

        var result = await prnCommonBackendService.UpdatePrnStatuses(
            organisationId,
            request.User.EffectiveId,
            request.EffectivePrnIds,
            request.Status.ToPrnCommonBackendStatus(),
            request.ObligationYear,
            cancellationToken
        );

        return result.ToResult();
    }

    // The common backend re-applies an update to a PRN already in the requested
    // status and records the year unconditionally, so a stale or replayed accept
    // would move an already-counted PRN into another year. When a year is sent,
    // every PRN must therefore still be awaiting acceptance. The year itself is
    // the caller's choice (the frontend applies the acceptance-year rules) and is
    // not checked against the PRNs. A small race remains between these reads and
    // the update.
    private static async Task<IResult?> CheckPrnsAreAwaitingAcceptance(
        Guid organisationId,
        IReadOnlyCollection<Guid> prnIds,
        IPrnCommonBackendService prnCommonBackendService,
        CancellationToken cancellationToken
    )
    {
        List<PrnData?> prns = [];

        // Reads are sent in small batches so a full selection does not open 100
        // backend requests at once.
        foreach (var batch in prnIds.Chunk(10))
        {
            prns.AddRange(
                await Task.WhenAll(
                    batch.Select(x =>
                        prnCommonBackendService.ReadPrn(organisationId, x.ToString("D"), cancellationToken)
                    )
                )
            );
        }

        if (prns.Any(x => x is null))
            return Results.NotFound();

        if (prns.Any(x => x!.PrnStatus != PrnCommonBackendStatus.AwaitingAcceptance))
            throw new ConcurrencyException("One or more PRNs are no longer awaiting acceptance.");

        return null;
    }
}
