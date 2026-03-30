using Defra.WasteObligations.Api.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Example;

public static class Get
{
    public static void MapExampleGet(this IEndpointRouteBuilder app)
    {
        app.MapGet("/example/{id:guid}", Handle).ExcludeFromDescription().RequireAuthorization(PolicyNames.Read);
    }

    [HttpGet]
    private static async Task<IResult> Handle(
        [FromRoute] Guid id,
        [FromQuery] bool? badRequest,
        CancellationToken cancellationToken
    )
    {
        await Task.Yield();

        if (badRequest.HasValue && badRequest.Value)
            throw new BadHttpRequestException("This is bad");

        return Results.Ok(id);
    }
}
