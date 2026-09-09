using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public static class ReadUnsubmittedOrganisationDetails
{
    public const string OperationId = "ReadUnsubmittedOrganisationDetails";

    public static void MapUnsubmittedOrganisationDetails(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/unsubmitted-compliance-declarations/organisations/{organisationId:guid}", Handle)
            .WithName(OperationId)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
    }

    private static async Task<IResult> Handle(
        [FromRoute] Guid organisationId,
        [FromServices] IUnsubmittedOrganisationDetailsService organisationDetailsService,
        CancellationToken cancellationToken,
        [FromQuery] bool includeLiveData = false
    )
    {
        var organisationDetails = await organisationDetailsService.Get(
            organisationId,
            includeLiveData,
            cancellationToken
        );

        return organisationDetails is null ? Results.NotFound() : Results.Ok(organisationDetails);
    }
}
