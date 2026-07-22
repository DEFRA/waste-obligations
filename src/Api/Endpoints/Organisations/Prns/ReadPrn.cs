using Defra.WasteObligations.Api.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Organisations.Prns;

public static class ReadPrn
{
    public static void MapPrnRead(this IEndpointRouteBuilder app)
    {
        app.MapGet("/organisations/{organisationId:guid}/prns/{prnId:guid}", Handle)
            .WithName("ReadOrganisationPrn")
            .WithTags("PRNs")
            .WithSummary("PRN by ID")
            .WithDescription("Return a PRN by organisation ID and PRN ID")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(PolicyNames.Read);
    }

    private static IResult Handle([FromRoute] Guid organisationId, [FromRoute] Guid prnId)
    {
        _ = (organisationId, prnId);

        return Results.NotFound();
    }
}
